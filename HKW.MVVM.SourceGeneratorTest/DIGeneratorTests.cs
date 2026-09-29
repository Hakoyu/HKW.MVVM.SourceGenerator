using HKW.MVVM.SourceGenerator;
using Microsoft.Extensions.DependencyInjection;

namespace HKW.MVVM.SourceGeneratorTest;

[TestClass]
public sealed class DIGeneratorTests
{
#pragma warning disable HKWDI006
    private static void RegisterWithoutConfiguration()
    {
        DIRegistrations.Register<IgnoredDependency>();
    }
#pragma warning restore HKWDI006

    [TestMethod]
    public void UsesSelectedConstructorAndInjectsProperty()
    {
        using var provider = TestServices.Build().BuildServiceProvider();

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

        var result = TestServices.Build(services);
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
        using var normalProvider = TestServices.Build().BuildServiceProvider();
        using var isolatedProvider = IsolatedServices.Build().BuildServiceProvider();

        Assert.IsNull(normalProvider.GetService<IsolatedDependency>());
        Assert.IsNotNull(isolatedProvider.GetService<IsolatedDependency>());
        Assert.IsNull(isolatedProvider.GetService<IInjectedService>());
        Assert.IsNull(normalProvider.GetService<IgnoredDependency>());
    }
}

[DIConfiguration]
public static partial class TestServices
{
    private static void Configure()
    {
        DIRegistrations.Register<ConstructorDependency>();
        DIRegistrations.Register<PropertyDependency>();
        DIRegistrations.Register<IPlugin, FirstPlugin>();
        DIRegistrations.Register<IPlugin, SecondPlugin>();
        DIRegistrations.RegisterLazySingleton<LazyDependency>();
        DIRegistrations.Register<IInjectedService, InjectedService>();
        DIRegistrations.RegisterScoped<ScopedDependency>();
    }
}

[DIConfiguration]
public static partial class IsolatedServices
{
    private static void Configure()
    {
        DIRegistrations.Register<IsolatedDependency>();
    }
}

public sealed class ConstructorDependency;

public sealed class PropertyDependency;

public sealed class LazyDependency;

public sealed class ScopedDependency;

public sealed class IgnoredDependency;

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
