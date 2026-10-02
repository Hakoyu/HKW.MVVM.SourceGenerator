using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 由依赖注入源生成器创建的轻量服务提供程序。
/// </summary>
public sealed class DIServiceProvider
    : IServiceProvider,
        IServiceScopeFactory,
        IServiceProviderIsService,
        IDisposable,
        IAsyncDisposable
{
    private static readonly AsyncLocal<ResolutionFrame?> _currentResolution = new();
    private readonly Dictionary<Type, Registration[]> _registrations = new();
    private readonly Dictionary<Type, Func<ScopeState, object?>> _serviceAccessors = new();
    private readonly ConcurrentDictionary<Type, Func<ScopeState, object?>> _enumerableAccessors =
        new();
    private readonly ServiceProviderOptions _options;
    private readonly ScopeState _rootScope;
    private int _disposed;

    /// <summary>
    /// 使用工厂型服务描述符初始化服务提供程序
    /// </summary>
    public DIServiceProvider(IEnumerable<ServiceDescriptor> serviceDescriptors)
        : this(
            serviceDescriptors,
            new ServiceProviderOptions(),
            Array.Empty<DIServiceRegistrationMetadata>()
        ) { }

    /// <summary>
    /// 使用工厂型服务描述符和验证选项初始化服务提供程序
    /// </summary>
    public DIServiceProvider(
        IEnumerable<ServiceDescriptor> serviceDescriptors,
        ServiceProviderOptions options
    )
        : this(serviceDescriptors, options, Array.Empty<DIServiceRegistrationMetadata>()) { }

    /// <summary>
    /// 使用工厂型服务描述符、验证选项和生成依赖元数据初始化服务提供程序
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public DIServiceProvider(
        IEnumerable<ServiceDescriptor> serviceDescriptors,
        ServiceProviderOptions options,
        IEnumerable<DIServiceRegistrationMetadata> metadata
    )
    {
        if (serviceDescriptors is null)
            throw new ArgumentNullException(nameof(serviceDescriptors));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (metadata is null)
            throw new ArgumentNullException(nameof(metadata));

        var registrations = new Dictionary<Type, List<Registration>>();
        var id = 0;
        foreach (var descriptor in serviceDescriptors)
        {
            if (
                descriptor.IsKeyedService
                || descriptor.ImplementationFactory is null
                || descriptor.ServiceType.ContainsGenericParameters
            )
            {
                throw new InvalidOperationException(
                    $"Registration for '{descriptor.ServiceType}' must use a non-keyed factory method and a closed service type."
                );
            }

            if (!registrations.TryGetValue(descriptor.ServiceType, out var services))
            {
                services = new List<Registration>();
                registrations.Add(descriptor.ServiceType, services);
            }
            services.Add(
                new Registration(
                    id++,
                    descriptor.ServiceType,
                    descriptor.Lifetime,
                    descriptor.ImplementationFactory
                )
            );
        }

        foreach (var pair in registrations)
        {
            var serviceRegistrations = pair.Value.ToArray();
            _registrations.Add(pair.Key, serviceRegistrations);
            var lastRegistration = serviceRegistrations[serviceRegistrations.Length - 1];
            _serviceAccessors.Add(pair.Key, scope => ResolveRegistration(lastRegistration, scope));
        }
        _rootScope = new ScopeState(this, true);
        if (_options.ValidateOnBuild)
            ValidateOnBuild(metadata);
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType)
    {
        if (serviceType is null)
            throw new ArgumentNullException(nameof(serviceType));
        return GetService(serviceType, _rootScope);
    }

    /// <summary>
    /// 创建服务作用域
    /// </summary>
    public DIServiceScope CreateScope()
    {
        ThrowIfDisposed();
        var scope = new ScopeState(this, false);
        return new DIServiceScope(scope.ServiceProvider, scope.Dispose, scope.DisposeAsync);
    }

    IServiceScope IServiceScopeFactory.CreateScope() => CreateScope();

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _rootScope.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return default;
        return _rootScope.DisposeAsync();
    }

    /// <inheritdoc />
    public bool IsService(Type serviceType)
    {
        if (serviceType is null)
            throw new ArgumentNullException(nameof(serviceType));
        return serviceType == typeof(IServiceProvider)
            || serviceType == typeof(IServiceScopeFactory)
            || serviceType == typeof(IServiceProviderIsService)
            || IsEnumerable(serviceType)
            || _registrations.ContainsKey(serviceType);
    }

    private object? GetService(Type serviceType, ScopeState scope)
    {
        ThrowIfDisposed();
        scope.ThrowIfDisposed();
        if (serviceType == typeof(IServiceProvider))
            return scope.ServiceProvider;
        if (serviceType == typeof(IServiceScopeFactory))
            return this;
        if (serviceType == typeof(IServiceProviderIsService))
            return this;
        if (_serviceAccessors.TryGetValue(serviceType, out var accessor))
            return accessor(scope);
        if (IsEnumerable(serviceType))
        {
            var elementType = serviceType.GetGenericArguments()[0];
            return _enumerableAccessors.GetOrAdd(elementType, CreateEnumerableAccessor)(scope);
        }
        return null;
    }

    private Func<ScopeState, object?> CreateEnumerableAccessor(Type elementType)
    {
        if (!_registrations.TryGetValue(elementType, out var registrations))
            return _ => Array.CreateInstance(elementType, 0);

        return scope =>
        {
            var services = Array.CreateInstance(elementType, registrations.Length);
            for (var index = 0; index < registrations.Length; index++)
                services.SetValue(ResolveRegistration(registrations[index], scope), index);
            return services;
        };
    }

    private object? ResolveRegistration(Registration registration, ScopeState scope)
    {
        ThrowIfDisposed();
        scope.ThrowIfDisposed();
        ValidateResolution(registration, scope);
        return registration.Lifetime switch
        {
            ServiceLifetime.Singleton => registration.GetOrCreateSingleton(
                _rootScope,
                () => CreateService(registration, _rootScope)
            ),
            ServiceLifetime.Scoped => scope.GetOrCreateScoped(
                registration,
                () => CreateService(registration, scope)
            ),
            _ => scope.CaptureDisposable(CreateService(registration, scope)),
        };
    }

    private object? CreateService(Registration registration, ScopeState scope)
    {
        using var resolution = EnterResolution(
            registration.Id,
            registration.ServiceType,
            registration.Lifetime
        );
        return registration.Factory(scope.ServiceProvider);
    }

    private void ValidateResolution(Registration registration, ScopeState scope)
    {
        if (!_options.ValidateScopes || registration.Lifetime != ServiceLifetime.Scoped)
            return;

        for (var frame = _currentResolution.Value; frame is not null; frame = frame.Parent)
        {
            if (
                ReferenceEquals(frame.Provider, this)
                && frame.Lifetime == ServiceLifetime.Singleton
            )
            {
                throw new InvalidOperationException(
                    $"Cannot consume scoped service '{registration.ServiceType}' from singleton '{frame.ServiceType}'."
                );
            }
        }

        if (scope.IsRoot)
        {
            throw new InvalidOperationException(
                $"Cannot resolve scoped service '{registration.ServiceType}' from root provider."
            );
        }
    }

    private void ValidateOnBuild(IEnumerable<DIServiceRegistrationMetadata> metadata)
    {
        var registrations = new Dictionary<Type, List<DIServiceRegistrationMetadata>>();
        foreach (var registration in metadata)
        {
            if (!registrations.TryGetValue(registration.ServiceType, out var services))
            {
                services = new List<DIServiceRegistrationMetadata>();
                registrations.Add(registration.ServiceType, services);
            }
            services.Add(registration);
        }

        List<Exception>? exceptions = null;
        foreach (var services in registrations.Values)
        {
            foreach (var registration in services)
            {
                try
                {
                    ValidateMetadata(
                        registration,
                        registrations,
                        new List<DIServiceRegistrationMetadata>(),
                        registration.Lifetime == ServiceLifetime.Singleton ? registration : null
                    );
                }
                catch (Exception exception)
                {
                    (exceptions ??= new List<Exception>()).Add(
                        new InvalidOperationException(
                            $"Error while validating service '{registration.ServiceType}': {exception.Message}",
                            exception
                        )
                    );
                }
            }
        }

        if (exceptions is not null)
            throw new AggregateException(
                "Some services are not able to be constructed",
                exceptions
            );
    }

    private void ValidateMetadata(
        DIServiceRegistrationMetadata registration,
        Dictionary<Type, List<DIServiceRegistrationMetadata>> registrations,
        List<DIServiceRegistrationMetadata> path,
        DIServiceRegistrationMetadata? singleton
    )
    {
        if (path.Contains(registration))
        {
            var cycle = path.SkipWhile(item => !ReferenceEquals(item, registration))
                .Select(item => item.ServiceType)
                .Concat(new[] { registration.ServiceType });
            throw new InvalidOperationException(
                $"A circular dependency was detected: {string.Join(" -> ", cycle)}"
            );
        }

        if (
            _options.ValidateScopes
            && singleton is not null
            && registration.Lifetime == ServiceLifetime.Scoped
        )
        {
            throw new InvalidOperationException(
                $"Cannot consume scoped service '{registration.ServiceType}' from singleton '{singleton.ServiceType}'."
            );
        }

        path.Add(registration);
        foreach (var dependency in registration.Dependencies)
        {
            if (IsBuiltInService(dependency.ServiceType))
                continue;
            if (!registrations.TryGetValue(dependency.ServiceType, out var candidates))
            {
                if (!dependency.IsEnumerable && !_registrations.ContainsKey(dependency.ServiceType))
                {
                    throw new InvalidOperationException(
                        $"No service for type '{dependency.ServiceType}' has been registered."
                    );
                }
                continue;
            }

            if (dependency.IsEnumerable)
            {
                foreach (var candidate in candidates)
                    ValidateMetadata(candidate, registrations, path, singleton);
            }
            else
            {
                ValidateMetadata(candidates[candidates.Count - 1], registrations, path, singleton);
            }
        }
        path.RemoveAt(path.Count - 1);
    }

    private static bool IsBuiltInService(Type serviceType) =>
        serviceType == typeof(IServiceProvider)
        || serviceType == typeof(IServiceScopeFactory)
        || serviceType == typeof(IServiceProviderIsService);

    private static bool IsEnumerable(Type serviceType) =>
        serviceType.IsGenericType
        && !serviceType.ContainsGenericParameters
        && serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>);

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(DIServiceProvider));
    }

    private IDisposable EnterResolution(
        int registrationId,
        Type serviceType,
        ServiceLifetime lifetime
    )
    {
        var current = _currentResolution.Value;
        for (var frame = current; frame is not null; frame = frame.Parent)
        {
            if (ReferenceEquals(frame.Provider, this) && frame.RegistrationId == registrationId)
                throw CreateCircularDependencyException(current, serviceType);
        }

        _currentResolution.Value = new ResolutionFrame(
            this,
            registrationId,
            serviceType,
            lifetime,
            current
        );
        return new ResolutionCookie(current);
    }

    private static InvalidOperationException CreateCircularDependencyException(
        ResolutionFrame? current,
        Type repeatedType
    )
    {
        var types = new List<Type>();
        for (var frame = current; frame is not null; frame = frame.Parent)
            types.Add(frame.ServiceType);
        types.Reverse();
        types.Add(repeatedType);
        return new InvalidOperationException(
            $"A circular dependency was detected: {string.Join(" -> ", types)}"
        );
    }

    private sealed class ScopeState : IDisposable, IAsyncDisposable
    {
        private readonly bool _isRoot;
        private readonly Dictionary<int, object?> _services = new();
        private readonly List<object> _disposables = new();
        private readonly HashSet<object> _captured = new(ReferenceEqualityComparer.Instance);
        private readonly object _syncRoot = new();
        private bool _disposed;

        internal ScopeState(DIServiceProvider owner, bool isRoot)
        {
            _isRoot = isRoot;
            ServiceProvider = isRoot ? owner : new ScopedServiceProvider(owner, this);
        }

        internal IServiceProvider ServiceProvider { get; }

        internal bool IsRoot => _isRoot;

        internal object? GetOrCreateScoped(Registration registration, Func<object?> factory)
        {
            lock (_syncRoot)
            {
                ThrowIfDisposedCore();
                if (_services.TryGetValue(registration.Id, out var existing))
                    return existing;

                var service = factory();
                CaptureDisposable(service);
                _services.Add(registration.Id, service);
                return service;
            }
        }

        internal object? CaptureDisposable(object? service)
        {
            if (service is not IDisposable && service is not IAsyncDisposable)
                return service;

            lock (_syncRoot)
            {
                if (!_disposed)
                {
                    if (_captured.Add(service))
                        _disposables.Add(service);
                    return service;
                }
            }

            DisposeRejectedService(service);
            throw CreateDisposedException();
        }

        internal void ThrowIfDisposed()
        {
            lock (_syncRoot)
                ThrowIfDisposedCore();
        }

        public void Dispose()
        {
            var disposables = BeginDispose();
            if (disposables is null)
                return;

            List<Exception>? exceptions = null;
            for (var index = disposables.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (disposables[index] is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    else
                    {
#pragma warning disable S3877
                        throw new InvalidOperationException(
                            $"'{disposables[index].GetType()}' only implements IAsyncDisposable. Use DisposeAsync to dispose the service provider."
                        );
#pragma warning restore S3877
                    }
                }
                catch (Exception exception)
                {
                    (exceptions ??= new()).Add(exception);
                }
            }
            ThrowDisposalExceptions(exceptions);
        }

        public async ValueTask DisposeAsync()
        {
            var disposables = BeginDispose();
            if (disposables is null)
                return;

            List<Exception>? exceptions = null;
            for (var index = disposables.Count - 1; index >= 0; index--)
            {
                try
                {
                    if (disposables[index] is IAsyncDisposable asyncDisposable)
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    else if (disposables[index] is IDisposable disposable)
                        disposable.Dispose();
                }
                catch (Exception exception)
                {
                    (exceptions ??= new List<Exception>()).Add(exception);
                }
            }
            ThrowDisposalExceptions(exceptions);
        }

        private List<object>? BeginDispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                    return null;
                _disposed = true;
                var disposables = new List<object>(_disposables);
                _disposables.Clear();
                _captured.Clear();
                _services.Clear();
                return disposables;
            }
        }

        private void ThrowIfDisposedCore()
        {
            if (_disposed)
                throw CreateDisposedException();
        }

        private ObjectDisposedException CreateDisposedException() =>
            new(_isRoot ? nameof(DIServiceProvider) : "IServiceScope");

        private static void ThrowDisposalExceptions(List<Exception>? exceptions)
        {
            if (exceptions is null)
                return;
            if (exceptions.Count == 1)
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            throw new AggregateException(exceptions);
        }

        private static void DisposeRejectedService(object service)
        {
            if (service is IDisposable disposable)
                disposable.Dispose();
            else if (service is IAsyncDisposable asyncDisposable)
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private sealed class ScopedServiceProvider(DIServiceProvider provider, ScopeState scope)
        : IServiceProvider
    {
        public object? GetService(Type serviceType) => provider.GetService(serviceType, scope);
    }

    private sealed class Registration(
        int id,
        Type serviceType,
        ServiceLifetime lifetime,
        Func<IServiceProvider, object> factory
    )
    {
        private readonly object _syncRoot = new();
        private bool _singletonResolved;
        private object? _singleton;

        internal int Id { get; } = id;
        internal Type ServiceType { get; } = serviceType;
        internal ServiceLifetime Lifetime { get; } = lifetime;
        internal Func<IServiceProvider, object> Factory { get; } = factory;

        internal object? GetOrCreateSingleton(ScopeState rootScope, Func<object?> create)
        {
            lock (_syncRoot)
            {
                if (_singletonResolved)
                    return _singleton;
                var service = create();
                rootScope.CaptureDisposable(service);
                _singleton = service;
                _singletonResolved = true;
                return service;
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceEqualityComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class ResolutionFrame(
        DIServiceProvider provider,
        int registrationId,
        Type serviceType,
        ServiceLifetime lifetime,
        ResolutionFrame? parent
    )
    {
        public DIServiceProvider Provider { get; } = provider;
        public int RegistrationId { get; } = registrationId;
        public Type ServiceType { get; } = serviceType;
        public ServiceLifetime Lifetime { get; } = lifetime;
        public ResolutionFrame? Parent { get; } = parent;
    }

    private sealed class ResolutionCookie(ResolutionFrame? previous) : IDisposable
    {
        public void Dispose() => _currentResolution.Value = previous;
    }
}

/// <summary>由 <see cref="DIServiceProvider"/> 创建的服务作用域
/// </summary>
public sealed class DIServiceScope : IServiceScope, IAsyncDisposable
{
    private readonly Action _dispose;
    private readonly Func<ValueTask> _disposeAsync;

    internal DIServiceScope(
        IServiceProvider serviceProvider,
        Action dispose,
        Func<ValueTask> disposeAsync
    )
    {
        ServiceProvider = serviceProvider;
        _dispose = dispose;
        _disposeAsync = disposeAsync;
    }

    /// <inheritdoc />
    public IServiceProvider ServiceProvider { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();

    /// <summary>
    /// 异步释放作用域及其创建的服务
    /// </summary>
    public ValueTask DisposeAsync() => _disposeAsync();
}
