using HKW.MVVM.SourceGenerator;
using Microsoft.Extensions.DependencyInjection;

namespace HKW.MVVM.SourceGeneratorTest;

#pragma warning disable S2094
[TestClass]
public sealed class DIGeneratorTests
{
    [TestMethod]
    public void UsesSelectedConstructorAndInjectsProperty()
    {
        using var provider = new TestServices().Build().BuildServiceProvider();

        var service = provider.GetRequiredService<IInjectedService>();

        Assert.IsNotNull(service.ConstructorDependency);
        Assert.IsNotNull(service.PropertyDependency);
        Assert.HasCount(3, service.Plugins);
        Assert.IsFalse(service.LazyDependency.IsValueCreated);
        Assert.IsNotNull(service.LazyDependency.Value);
    }

    [TestMethod]
    public void HonorsGeneratedLifetimes()
    {
        var services = new ServiceCollection();

        var result = new TestServices().Build(services);
        using var provider = result.BuildServiceProvider();

        Assert.AreSame(services, result);
        Assert.IsNull(provider.GetService<IgnoredDependency>());

        Assert.AreNotSame(
            provider.GetRequiredService<IInjectedService>(),
            provider.GetRequiredService<IInjectedService>()
        );
        Assert.AreSame(
            provider.GetRequiredService<LazyDependency>(),
            provider.GetRequiredService<LazyDependency>()
        );

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        Assert.AreSame(
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>(),
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>()
        );
        Assert.AreNotSame(
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>(),
            secondScope.ServiceProvider.GetRequiredService<ScopedDependency>()
        );
    }

    [TestMethod]
    public void RegistrationsAreScopedToConfigurationType()
    {
        using var normalProvider = new TestServices().Build().BuildServiceProvider();
        using var isolatedProvider = new IsolatedServices().Build().BuildServiceProvider();
        using var baseOnlyProvider = new BaseOnlyServices().Build().BuildServiceProvider();

        Assert.IsNull(normalProvider.GetService<IsolatedDependency>());
        Assert.IsNotNull(isolatedProvider.GetService<IsolatedDependency>());
        Assert.IsNull(isolatedProvider.GetService<IInjectedService>());
        Assert.IsNull(normalProvider.GetService<IgnoredDependency>());
        Assert.IsNotNull(baseOnlyProvider.GetService<IgnoredDependency>());
    }

    [TestMethod]
    public void ConfigurationRunsCustomOperationsAndOverridesRegistration()
    {
        var configuration = new TestServices();
        var services = configuration.Build();
        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<CustomDependency>());
        Assert.IsTrue(configuration.RegistrationCount > 0);
    }

    [TestMethod]
    public void CustomServiceRegistrarRegistersSpecifiedGenericParameters()
    {
        var configuration = new CustomRegistrarServices();
        using var provider = configuration.Build().BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<FirstCustomRegistrarDependency>());
        Assert.IsNull(provider.GetService<UnselectedCustomRegistrarDependency>());
        Assert.IsNotNull(provider.GetService<SecondCustomRegistrarDependency>());
        Assert.IsNotNull(provider.GetService<AnotherFirstCustomRegistrarDependency>());
        Assert.IsNull(provider.GetService<AnotherUnselectedCustomRegistrarDependency>());
        Assert.IsNotNull(provider.GetService<AnotherSecondCustomRegistrarDependency>());
        Assert.AreEqual(2, configuration.InvocationCount);
    }

    [TestMethod]
    public void DuplicateRegistrationsKeepOnlyTheFirst()
    {
        using var provider = new DuplicateRegistrationServices().Build().BuildServiceProvider();

        Assert.HasCount(1, provider.GetServices<DuplicateDependency>());
    }

    [TestMethod]
    public void CustomServiceRegistrarHonorsIndividualRegistrationModes()
    {
        using var provider = new CustomRegistrarLifetimeServices().Build().BuildServiceProvider();

        Assert.AreNotSame(
            provider.GetRequiredService<CustomTransientDependency>(),
            provider.GetRequiredService<CustomTransientDependency>()
        );
        Assert.AreSame(
            provider.GetRequiredService<CustomSingletonDependency>(),
            provider.GetRequiredService<CustomSingletonDependency>()
        );
        Assert.AreSame(
            provider.GetRequiredService<CustomLazySingletonDependency>(),
            provider.GetRequiredService<CustomLazySingletonDependency>()
        );

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        Assert.AreSame(
            firstScope.ServiceProvider.GetRequiredService<CustomScopedDependency>(),
            firstScope.ServiceProvider.GetRequiredService<CustomScopedDependency>()
        );
        Assert.AreNotSame(
            firstScope.ServiceProvider.GetRequiredService<CustomScopedDependency>(),
            secondScope.ServiceProvider.GetRequiredService<CustomScopedDependency>()
        );
    }

    [TestMethod]
    public void GeneratedProviderSupportsManualFactoryDescriptors()
    {
        var configuration = new TestServices();
        var invocationCount = configuration.RegistrationCount;
        using var provider = configuration.BuildServiceProvider();

        var service = provider.GetRequiredService<IInjectedService>();

        Assert.IsNotNull(service.ConstructorDependency);
        Assert.IsNotNull(service.PropertyDependency);
        Assert.HasCount(3, service.Plugins);
        Assert.IsFalse(service.LazyDependency.IsValueCreated);
        Assert.IsNotNull(service.LazyDependency.Value);
        Assert.AreNotSame(service, provider.GetRequiredService<IInjectedService>());
        Assert.AreSame(
            provider.GetRequiredService<LazyDependency>(),
            provider.GetRequiredService<LazyDependency>()
        );
        Assert.IsInstanceOfType<SecondPlugin>(provider.GetRequiredService<IPlugin>());
        Assert.AreSame(
            provider.GetRequiredService<CustomDependency>(),
            provider.GetRequiredService<CustomDependency>()
        );
        var manualFactoryService = provider.GetRequiredService<ManualFactoryDependency>();
        Assert.IsNotNull(manualFactoryService.ConstructorDependency);
        Assert.AreNotSame(
            manualFactoryService,
            provider.GetRequiredService<ManualFactoryDependency>()
        );
        Assert.IsTrue(configuration.RegistrationCount > invocationCount);
        Assert.AreSame(provider, provider.GetRequiredService<IServiceProvider>());
        Assert.AreSame(provider, provider.GetRequiredService<IServiceScopeFactory>());

        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        Assert.AreSame(
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>(),
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>()
        );
        Assert.AreNotSame(
            firstScope.ServiceProvider.GetRequiredService<ScopedDependency>(),
            secondScope.ServiceProvider.GetRequiredService<ScopedDependency>()
        );
    }

    [TestMethod]
    public void GeneratedProviderCreatesSingletonOnceAcrossThreads()
    {
        ConcurrentSingleton.Reset();
        using var provider = new ProviderRuntimeServices().BuildServiceProvider();
        var instances = new ConcurrentSingleton[32];

        Parallel.For(
            0,
            instances.Length,
            index => instances[index] = provider.GetRequiredService<ConcurrentSingleton>()
        );

        Assert.AreEqual(1, ConcurrentSingleton.ConstructorCount);
        Assert.IsTrue(instances.All(instance => ReferenceEquals(instance, instances[0])));
    }

    [TestMethod]
    public void GeneratedProviderDetectsCircularDependencies()
    {
        var services = new ServiceCollection();
        services.AddTransient<CircularA>(provider => new CircularA(
            provider.GetRequiredService<CircularB>()
        ));
        services.AddTransient<CircularB>(provider => new CircularB(
            provider.GetRequiredService<CircularA>()
        ));
        using var provider = new DIServiceProvider(services);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<CircularA>()
        );

        Assert.Contains(nameof(CircularA), exception.Message);
        Assert.Contains(nameof(CircularB), exception.Message);
    }

    [TestMethod]
    public void GeneratedProviderRejectsManualRegistrationsWithoutFactories()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new UnsupportedManualServices().BuildServiceProvider()
        );

        Assert.Contains("factory method", exception.Message);
    }

    [TestMethod]
    public void ValidateOnBuildDoesNotAnalyzeManualFactories()
    {
        using var provider = new OpaqueManualFactoryServices().BuildServiceProvider(
            new HKW.MVVM.SourceGenerator.ServiceProviderOptions { ValidateOnBuild = true }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<OpaqueManualFactoryDependency>()
        );

        Assert.Contains(nameof(IgnoredDependency), exception.Message);
    }

    [TestMethod]
    public async Task GeneratedProviderOwnsAndDisposesCreatedServices()
    {
        DisposalEvents.Reset();
        var provider = new ProviderRuntimeServices().BuildServiceProvider();
        var singleton = provider.GetRequiredService<DisposableSingleton>();
        var asyncOnly = provider.GetRequiredService<AsyncOnlyDisposable>();
        var rootTransient = provider.GetRequiredService<DisposableTransient>();
        var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<DisposableScoped>();
        var scopedTransient = scope.ServiceProvider.GetRequiredService<DisposableTransient>();

        scope.Dispose();

        Assert.IsTrue(scoped.IsDisposed);
        Assert.IsTrue(scopedTransient.IsDisposed);
        Assert.IsFalse(singleton.IsDisposed);
        Assert.IsFalse(rootTransient.IsDisposed);

        await provider.DisposeAsync();

        Assert.IsTrue(singleton.IsDisposed);
        Assert.IsTrue(asyncOnly.IsDisposed);
        Assert.IsTrue(rootTransient.IsDisposed);
        Assert.AreSequenceEqual(
            new[]
            {
                nameof(DisposableTransient),
                nameof(DisposableScoped),
                nameof(DisposableTransient),
                nameof(AsyncOnlyDisposable),
                nameof(DisposableSingleton),
            },
            DisposalEvents.Snapshot()
        );
        Assert.Throws<ObjectDisposedException>(() =>
            provider.GetService(typeof(DisposableSingleton))
        );
    }

    [TestMethod]
    public void FactoryNullBehaviorMatchesMicrosoftProviderAndCachesSingleton()
    {
        var lightweightCalls = 0;
        var lightweightServices = new ServiceCollection();
        lightweightServices.AddSingleton<NullFactoryService>(_ =>
        {
            lightweightCalls++;
            return null!;
        });
        using var lightweight = new DIServiceProvider(lightweightServices);

        var microsoftCalls = 0;
        var microsoftServices = new ServiceCollection();
        microsoftServices.AddSingleton<NullFactoryService>(_ =>
        {
            microsoftCalls++;
            return null!;
        });
        using var microsoft = microsoftServices.BuildServiceProvider();

        Assert.IsNull(lightweight.GetService<NullFactoryService>());
        Assert.IsNull(lightweight.GetService<NullFactoryService>());
        Assert.IsNull(microsoft.GetService<NullFactoryService>());
        Assert.IsNull(microsoft.GetService<NullFactoryService>());
        Assert.AreEqual(microsoftCalls, lightweightCalls);
        Assert.AreEqual(1, lightweightCalls);
    }

    [TestMethod]
    public void ExplicitEnumerableRegistrationMatchesMicrosoftProvider()
    {
        var lightweightExplicit = new EnumerableService("lightweight-explicit");
        var lightweightElement = new EnumerableService("lightweight-element");
        var lightweightServices = new ServiceCollection();
        lightweightServices.AddSingleton<IEnumerable<EnumerableService>>(_ =>
            new[] { lightweightExplicit }
        );
        lightweightServices.AddSingleton(_ => lightweightElement);
        using var lightweight = new DIServiceProvider(lightweightServices);

        var microsoftExplicit = new EnumerableService("microsoft-explicit");
        var microsoftElement = new EnumerableService("microsoft-element");
        var microsoftServices = new ServiceCollection();
        microsoftServices.AddSingleton<IEnumerable<EnumerableService>>(_ =>
            new[] { microsoftExplicit }
        );
        microsoftServices.AddSingleton(_ => microsoftElement);
        using var microsoft = microsoftServices.BuildServiceProvider();

        var lightweightResult = lightweight.GetRequiredService<IEnumerable<EnumerableService>>();
        var microsoftResult = microsoft.GetRequiredService<IEnumerable<EnumerableService>>();
        Assert.AreEqual(microsoftResult.Count(), lightweightResult.Count());
        Assert.AreEqual(
            ReferenceEquals(microsoftResult.Single(), microsoftExplicit),
            ReferenceEquals(lightweightResult.Single(), lightweightExplicit)
        );
    }

    [TestMethod]
    public void ValidateScopesRejectsRootScopedResolution()
    {
        using var provider = new ProviderRuntimeServices().BuildServiceProvider(
            new HKW.MVVM.SourceGenerator.ServiceProviderOptions { ValidateScopes = true }
        );

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<DisposableScoped>()
        );
    }

    [TestMethod]
    public void ValidateScopesRejectsSingletonCapturingScopedService()
    {
        using var provider = CreateCaptiveProvider(
            new HKW.MVVM.SourceGenerator.ServiceProviderOptions { ValidateScopes = true }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<CaptiveSingleton>()
        );

        Assert.Contains(nameof(CaptiveScoped), exception.Message);
        Assert.Contains(nameof(CaptiveSingleton), exception.Message);
    }

    [TestMethod]
    public void ValidateOnBuildAggregatesKnownGraphErrorsWithoutRunningFactories()
    {
        CaptiveSingleton.Reset();

        var exception = Assert.Throws<AggregateException>(() =>
            CreateCaptiveProvider(
                new HKW.MVVM.SourceGenerator.ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                }
            )
        );

        Assert.AreEqual(0, CaptiveSingleton.ConstructorCount);
        Assert.Contains(nameof(CaptiveScoped), exception.ToString());
    }

    [TestMethod]
    public void GeneratedProviderIsConcreteAndReportsAvailableServices()
    {
        using var provider = new TestServices().BuildServiceProvider();
        var serviceChecker = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.AreEqual(typeof(DIServiceProvider), provider.GetType());
        Assert.IsFalse(typeof(DIServiceProvider).IsAbstract);
        Assert.IsTrue(typeof(DIServiceProvider).IsSealed);
        Assert.AreSame(provider, serviceChecker);
        Assert.IsTrue(serviceChecker.IsService(typeof(IInjectedService)));
        Assert.IsTrue(serviceChecker.IsService(typeof(IEnumerable<IgnoredDependency>)));
        Assert.IsFalse(serviceChecker.IsService(typeof(IEnumerable<>)));
        Assert.IsFalse(serviceChecker.IsService(typeof(IgnoredDependency)));
        Assert.IsEmpty(provider.GetServices<IgnoredDependency>());
    }

    [TestMethod]
    public async Task TransientFactoriesCanRunConcurrently()
    {
        using var barrier = new Barrier(2);
        using var provider = new DIServiceProvider(
            new ServiceCollection().AddTransient(_ =>
            {
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Transient factories were serialized.");
                return new ConcurrentTransient();
            })
        );

        await Task.WhenAll(
            Task.Run(() => provider.GetRequiredService<ConcurrentTransient>()),
            Task.Run(() => provider.GetRequiredService<ConcurrentTransient>())
        );
    }

    [TestMethod]
    public async Task ServiceCreatedAfterDisposalIsDisposedAndRejected()
    {
        using var created = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var service = new RaceDisposable();
        var provider = new DIServiceProvider(
            new ServiceCollection().AddTransient(_ =>
            {
                created.Set();
                release.Wait();
                return service;
            })
        );
        var resolution = Task.Run(() => provider.GetRequiredService<RaceDisposable>());
        Assert.IsTrue(created.Wait(TimeSpan.FromSeconds(5)));

        provider.Dispose();
        release.Set();

        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await resolution;
        });
        Assert.IsTrue(service.IsDisposed);
    }

    [TestMethod]
    public void SharedDisposableIsCapturedOnlyOnce()
    {
        var shared = new CountingDisposable("shared", false);
        var services = new ServiceCollection();
        services.AddTransient(_ => shared);
        services.AddTransient(_ => shared);
        var provider = new DIServiceProvider(services);

        Assert.HasCount(2, provider.GetServices<CountingDisposable>());
        provider.Dispose();

        Assert.AreEqual(1, shared.DisposeCount);
    }

    [TestMethod]
    public void DisposalContinuesInReverseOrderAndAggregatesFailures()
    {
        var disposalOrder = new List<string>();
        var first = new CountingDisposable("first", true, disposalOrder.Add);
        var second = new CountingDisposable("second", true, disposalOrder.Add);
        var services = new ServiceCollection();
        services.AddTransient(_ => first);
        services.AddTransient(_ => second);
        var provider = new DIServiceProvider(services);
        _ = provider.GetServices<CountingDisposable>().ToArray();

        var exception = Assert.Throws<AggregateException>(provider.Dispose);

        Assert.HasCount(2, exception.InnerExceptions);
        CollectionAssert.AreEqual(new[] { "second", "first" }, disposalOrder);
    }

    private static DIServiceProvider CreateCaptiveProvider(
        HKW.MVVM.SourceGenerator.ServiceProviderOptions options
    )
    {
        var services = new ServiceCollection();
        services.AddScoped<CaptiveScoped>(_ => new CaptiveScoped());
        services.AddSingleton<CaptiveSingleton>(provider => new CaptiveSingleton(
            provider.GetRequiredService<CaptiveScoped>()
        ));
        var metadata = new[]
        {
            new DIServiceRegistrationMetadata(typeof(CaptiveScoped), ServiceLifetime.Scoped, []),
            new DIServiceRegistrationMetadata(
                typeof(CaptiveSingleton),
                ServiceLifetime.Singleton,
                [new DIServiceDependencyMetadata(typeof(CaptiveScoped))]
            ),
        };
        return new DIServiceProvider(services, options, metadata);
    }
}

public partial class TestServices : DIConfigurationBase
{
    public int RegistrationCount { get; private set; }

    protected override void Configure(IServiceCollection services)
    {
        services.AddSingleton(_ => new CustomDependency());
        services.AddTransient(serviceProvider => new ManualFactoryDependency(
            serviceProvider.GetRequiredService<ConstructorDependency>()
        ));
        services.AddTransient<IPlugin>(_ => new ManualPlugin());
        Register<ConstructorDependency>();
        Register<PropertyDependency>();
        Register<IPlugin, FirstPlugin>();
        Register<IPlugin, SecondPlugin>();
        RegisterLazySingleton<LazyDependency>();
        Register<IInjectedService, InjectedService>();
        RegisterScoped<ScopedDependency>();
    }

    protected override void Register<T>(
        IServiceCollection services,
        Func<IServiceProvider, T> factory
    )
    {
        RegistrationCount++;
        base.Register(services, factory);
    }
}

public partial class IsolatedServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        Register<IsolatedDependency>();
    }
}

public sealed partial class BaseOnlyServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        Register<IgnoredDependency>();
    }
}

public sealed partial class CustomRegistrarServices : DIConfigurationBase
{
    public int InvocationCount { get; private set; }

    protected override void Configure(IServiceCollection services)
    {
        RegisterCustom<
            FirstCustomRegistrarDependency,
            UnselectedCustomRegistrarDependency,
            SecondCustomRegistrarDependency
        >();
        RegisterCustom<
            AnotherFirstCustomRegistrarDependency,
            AnotherUnselectedCustomRegistrarDependency,
            AnotherSecondCustomRegistrarDependency
        >();
    }

    [DICustomServiceRegistrar(nameof(TFirst), nameof(TSecond))]
    private void RegisterCustom<TFirst, TUnselected, TSecond>()
        where TFirst : class
        where TUnselected : class
        where TSecond : class
    {
        InvocationCount++;
        Register<TFirst>();
        Register<TUnselected>();
        Register<TSecond>();
    }
}

public sealed partial class DuplicateRegistrationServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        Register<DuplicateDependency>();
        Register<DuplicateDependency>();
    }
}

public sealed partial class CustomRegistrarLifetimeServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        RegisterCustom<
            CustomTransientDependency,
            CustomScopedDependency,
            CustomSingletonDependency,
            CustomLazySingletonDependency
        >();
    }

    [DICustomServiceRegistrar(
        [nameof(TTransient), nameof(TScoped), nameof(TSingleton), nameof(TLazySingleton)],
        [
            DIServiceRegistration.Normal,
            DIServiceRegistration.Scoped,
            DIServiceRegistration.Singleton,
            DIServiceRegistration.LazySingleton,
        ]
    )]
    private void RegisterCustom<TTransient, TScoped, TSingleton, TLazySingleton>()
        where TTransient : class
        where TScoped : class
        where TSingleton : class
        where TLazySingleton : class { }
}

public sealed partial class ProviderRuntimeServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        RegisterSingleton<ConcurrentSingleton>();
        RegisterSingleton<DisposableSingleton>();
        RegisterSingleton<AsyncOnlyDisposable>();
        Register<DisposableTransient>();
        RegisterScoped<DisposableScoped>();
    }
}

public sealed partial class UnsupportedManualServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        services.AddSingleton<CustomDependency>();
    }
}

public sealed partial class OpaqueManualFactoryServices : DIConfigurationBase
{
    protected override void Configure(IServiceCollection services)
    {
        services.AddTransient(provider => new OpaqueManualFactoryDependency(
            provider.GetRequiredService<IgnoredDependency>()
        ));
    }
}

public sealed class ConstructorDependency;

public sealed class PropertyDependency;

public sealed class LazyDependency;

public sealed class ScopedDependency;

public sealed class IgnoredDependency;

public sealed class CustomDependency;

public sealed class ManualFactoryDependency(ConstructorDependency constructorDependency)
{
    public ConstructorDependency ConstructorDependency { get; } = constructorDependency;
}

public sealed class OpaqueManualFactoryDependency(IgnoredDependency dependency)
{
    public IgnoredDependency Dependency { get; } = dependency;
}

public sealed class FirstCustomRegistrarDependency;

public sealed class UnselectedCustomRegistrarDependency;

public sealed class SecondCustomRegistrarDependency;

public sealed class AnotherFirstCustomRegistrarDependency;

public sealed class AnotherUnselectedCustomRegistrarDependency;

public sealed class AnotherSecondCustomRegistrarDependency;

public sealed class DuplicateDependency;

public sealed class CustomTransientDependency;

public sealed class CustomScopedDependency;

public sealed class CustomSingletonDependency;

public sealed class CustomLazySingletonDependency;

public sealed class IsolatedDependency;

public sealed class ConcurrentSingleton
{
    private static int _constructorCount;

    public ConcurrentSingleton()
    {
        Interlocked.Increment(ref _constructorCount);
    }

    public static int ConstructorCount => Volatile.Read(ref _constructorCount);

    public static void Reset() => Interlocked.Exchange(ref _constructorCount, 0);
}

public sealed class ConcurrentTransient;

public sealed class RaceDisposable : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

public sealed class CountingDisposable(
    string name,
    bool throwOnDispose,
    Action<string>? onDispose = null
) : IDisposable
{
    private int _disposeCount;

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public void Dispose()
    {
        Interlocked.Increment(ref _disposeCount);
        onDispose?.Invoke(name);
        if (throwOnDispose)
            throw new InvalidOperationException(name);
    }
}

public static class DisposalEvents
{
    private static readonly List<string> Events = [];

    public static void Add(string value)
    {
        lock (Events)
            Events.Add(value);
    }

    public static void Reset()
    {
        lock (Events)
            Events.Clear();
    }

    public static string[] Snapshot()
    {
        lock (Events)
            return Events.ToArray();
    }
}

public sealed class DisposableSingleton : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalEvents.Add(nameof(DisposableSingleton));
    }
}

public sealed class DisposableScoped : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalEvents.Add(nameof(DisposableScoped));
    }
}

public sealed class DisposableTransient : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        IsDisposed = true;
        DisposalEvents.Add(nameof(DisposableTransient));
    }
}

public sealed class AsyncOnlyDisposable : IAsyncDisposable
{
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        DisposalEvents.Add(nameof(AsyncOnlyDisposable));
        return default;
    }
}

public sealed class NullFactoryService;

public sealed class EnumerableService(string name)
{
    public string Name { get; } = name;
}

public sealed class CaptiveScoped;

public sealed class CaptiveSingleton
{
    private static int _constructorCount;

    public CaptiveSingleton(CaptiveScoped dependency)
    {
        Dependency = dependency;
        Interlocked.Increment(ref _constructorCount);
    }

    public CaptiveScoped Dependency { get; }

    public static int ConstructorCount => Volatile.Read(ref _constructorCount);

    public static void Reset() => Interlocked.Exchange(ref _constructorCount, 0);
}

public sealed class CircularA(CircularB dependency)
{
    public CircularB Dependency { get; } = dependency;
}

public sealed class CircularB(CircularA dependency)
{
    public CircularA Dependency { get; } = dependency;
}

public interface IPlugin;

public sealed class ManualPlugin : IPlugin;

public sealed class FirstPlugin : IPlugin;

public sealed class SecondPlugin : IPlugin;

public interface IInjectedService
{
    ConstructorDependency ConstructorDependency { get; }
    PropertyDependency PropertyDependency { get; }
    IReadOnlyCollection<IPlugin> Plugins { get; }
    Lazy<LazyDependency> LazyDependency { get; }
}

public sealed class InjectedService : IInjectedService
{
    public InjectedService() => throw new InvalidOperationException("Wrong constructor selected.");

    [DIConstructor]
    internal InjectedService(
        ConstructorDependency constructorDependency,
        IEnumerable<IPlugin> plugins,
        Lazy<LazyDependency> lazyDependency
    )
    {
        ConstructorDependency = constructorDependency;
        Plugins = plugins.ToArray();
        LazyDependency = lazyDependency;
    }

    public ConstructorDependency ConstructorDependency { get; }

    [DIProperty]
    public PropertyDependency PropertyDependency { get; init; } = null!;

    public IReadOnlyCollection<IPlugin> Plugins { get; }

    public Lazy<LazyDependency> LazyDependency { get; }
}
#pragma warning restore S2094
