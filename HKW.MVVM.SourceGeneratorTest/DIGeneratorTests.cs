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
        using var provider = TestServices.Instance.Build().BuildServiceProvider();

        var service = provider.GetRequiredService<IInjectedService>();

        Assert.IsNotNull(service.ConstructorDependency);
        Assert.IsNotNull(service.PropertyDependency);
        Assert.HasCount(2, service.Plugins);
        Assert.IsFalse(service.LazyDependency.IsValueCreated);
        Assert.IsNotNull(service.LazyDependency.Value);
    }

    [TestMethod]
    public void HonorsGeneratedLifetimes()
    {
        var services = new ServiceCollection();

        var result = TestServices.Instance.Build(services);
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
        using var normalProvider = TestServices.Instance.Build().BuildServiceProvider();
        using var isolatedProvider = IsolatedServices.Instance.Build().BuildServiceProvider();
        using var baseOnlyProvider = BaseOnlyServices.Instance.Build().BuildServiceProvider();

        Assert.IsNull(normalProvider.GetService<IsolatedDependency>());
        Assert.IsNotNull(isolatedProvider.GetService<IsolatedDependency>());
        Assert.IsNull(isolatedProvider.GetService<IInjectedService>());
        Assert.IsNull(normalProvider.GetService<IgnoredDependency>());
        Assert.IsNotNull(baseOnlyProvider.GetService<IgnoredDependency>());
    }

    [TestMethod]
    public void ConfigurationRunsCustomOperationsAndOverridesRegistration()
    {
        var services = TestServices.Instance.Build();
        using var provider = services.BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<CustomDependency>());
        Assert.IsTrue(TestServices.Instance.RegistrationCount > 0);
    }

    [TestMethod]
    public void ConfigurationInstanceIsCreatedLazily()
    {
        Assert.AreEqual(0, LazyConfiguration.ConstructorCount);

        var first = LazyConfiguration.Instance;
        var second = LazyConfiguration.Instance;

        Assert.AreSame(first, second);
        Assert.AreEqual(1, LazyConfiguration.ConstructorCount);
    }

    [TestMethod]
    public void CustomServiceRegistrarRegistersSpecifiedGenericParameters()
    {
        using var provider = CustomRegistrarServices.Instance.Build().BuildServiceProvider();

        Assert.IsNotNull(provider.GetService<FirstCustomRegistrarDependency>());
        Assert.IsNull(provider.GetService<UnselectedCustomRegistrarDependency>());
        Assert.IsNotNull(provider.GetService<SecondCustomRegistrarDependency>());
        Assert.AreEqual(1, CustomRegistrarServices.Instance.InvocationCount);
    }

    [TestMethod]
    public void CustomServiceRegistrarHonorsIndividualRegistrationModes()
    {
        using var provider = CustomRegistrarLifetimeServices
            .Instance.Build()
            .BuildServiceProvider();

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
}

public partial class TestServices : DIConfigurationBase
{
    public int RegistrationCount { get; private set; }

    protected override void Configure(IServiceCollection services)
    {
        services.AddSingleton<CustomDependency>();
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

public sealed partial class LazyConfiguration : DIConfigurationBase
{
    internal LazyConfiguration()
    {
        ConstructorCount++;
    }

    public static int ConstructorCount { get; private set; }

    protected override void Configure(IServiceCollection services) { }
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

public sealed class ConstructorDependency;

public sealed class PropertyDependency;

public sealed class LazyDependency;

public sealed class ScopedDependency;

public sealed class IgnoredDependency;

public sealed class CustomDependency;

public sealed class FirstCustomRegistrarDependency;

public sealed class UnselectedCustomRegistrarDependency;

public sealed class SecondCustomRegistrarDependency;

public sealed class CustomTransientDependency;

public sealed class CustomScopedDependency;

public sealed class CustomSingletonDependency;

public sealed class CustomLazySingletonDependency;

public sealed class IsolatedDependency;

public interface IPlugin;

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
