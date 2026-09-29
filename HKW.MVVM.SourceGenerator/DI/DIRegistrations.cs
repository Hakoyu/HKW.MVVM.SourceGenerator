using System.ComponentModel;

namespace HKW.MVVM.SourceGenerator;

#pragma warning disable S1186, S2326
/// <summary>
/// Declares registrations consumed at compile time by the dependency injection source generator.
/// </summary>
/// <remarks>
/// Calls to these methods are compile-time markers. Call <c>AddGeneratedServices</c> on an
/// <c>IServiceCollection</c> to apply the generated registrations.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DIRegistrations
{
    /// <summary>Declares a transient self-registration.</summary>
    public static void Register<T>()
        where T : class { }

    /// <summary>Declares a transient service registration.</summary>
    public static void Register<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>Declares a scoped self-registration.</summary>
    public static void RegisterScoped<T>()
        where T : class { }

    /// <summary>Declares a scoped service registration.</summary>
    public static void RegisterScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>Declares a singleton self-registration.</summary>
    public static void RegisterSingleton<T>()
        where T : class { }

    /// <summary>Declares a singleton service registration.</summary>
    public static void RegisterSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>Declares a lazily-created singleton self-registration.</summary>
    public static void RegisterLazySingleton<T>()
        where T : class { }

    /// <summary>Declares a lazily-created singleton service registration.</summary>
    public static void RegisterLazySingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }
}
#pragma warning restore S1186, S2326
