using System.ComponentModel;

namespace HKW.MVVM.SourceGenerator;

#pragma warning disable S1186, S2326
/// <summary>
/// 声明由依赖注入源生成器在编译时收集的服务注册
/// </summary>
/// <remarks>
/// 注册方法仅作为编译时标记，不会在运行时执行注册
/// 请在标记了 <see cref="DIConfigurationAttribute"/> 的静态分部类中使用
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DIRegistrations
{
    /// <summary>声明瞬态自注册</summary>
    public static void Register<T>()
        where T : class { }

    /// <summary>声明瞬态服务注册</summary>
    public static void Register<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明作用域自注册</summary>
    public static void RegisterScoped<T>()
        where T : class { }

    /// <summary>声明作用域服务注册</summary>
    public static void RegisterScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明单例自注册</summary>
    public static void RegisterSingleton<T>()
        where T : class { }

    /// <summary>声明单例服务注册</summary>
    public static void RegisterSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明延迟创建的单例自注册</summary>
    public static void RegisterLazySingleton<T>()
        where T : class { }

    /// <summary>声明延迟创建的单例服务注册</summary>
    public static void RegisterLazySingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }
}
#pragma warning restore S1186, S2326
