using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace HKW.MVVM.SourceGenerator;

#pragma warning disable S1186, S2326
/// <summary>
/// 声明由依赖注入源生成器在编译时收集的服务注册
/// </summary>
/// <remarks>
/// 无参数的注册方法仅作为编译时标记；生成的注册代码会调用可重写的工厂注册方法。
/// 派生配置类必须是顶层、非泛型、非抽象的分部类，并具有可访问的无参数构造函数。
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class DIConfigurationBase
{
    /// <summary>创建服务集合并应用源生成注册</summary>
    public IServiceCollection Build() => Build(new ServiceCollection());

    /// <summary>将源生成注册应用到指定服务集合</summary>
    public virtual IServiceCollection Build(IServiceCollection services)
    {
        return services;
    }

    /// <summary>在源生成注册之前执行其他配置操作</summary>
    protected abstract void Configure(IServiceCollection services);

    /// <summary>声明瞬态自注册</summary>
    protected void Register<T>()
        where T : class { }

    /// <summary>声明瞬态服务注册</summary>
    protected void Register<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明作用域自注册</summary>
    protected void RegisterScoped<T>()
        where T : class { }

    /// <summary>声明作用域服务注册</summary>
    protected void RegisterScoped<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明单例自注册</summary>
    protected void RegisterSingleton<T>()
        where T : class { }

    /// <summary>声明单例服务注册</summary>
    protected void RegisterSingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>声明延迟创建的单例自注册</summary>
    protected void RegisterLazySingleton<T>()
        where T : class { }

    /// <summary>声明延迟创建的单例服务注册</summary>
    protected void RegisterLazySingleton<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService { }

    /// <summary>执行瞬态自注册，可在派生类中重写</summary>
    protected virtual void Register<T>(
        IServiceCollection services,
        Func<IServiceProvider, T> factory
    )
        where T : class => services.AddTransient(factory);

    /// <summary>执行瞬态服务注册，可在派生类中重写</summary>
    protected virtual void Register<TService, TImplementation>(
        IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService => services.AddTransient<TService>(factory);

    /// <summary>执行作用域自注册，可在派生类中重写</summary>
    protected virtual void RegisterScoped<T>(
        IServiceCollection services,
        Func<IServiceProvider, T> factory
    )
        where T : class => services.AddScoped(factory);

    /// <summary>执行作用域服务注册，可在派生类中重写</summary>
    protected virtual void RegisterScoped<TService, TImplementation>(
        IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService => services.AddScoped<TService>(factory);

    /// <summary>执行单例自注册，可在派生类中重写</summary>
    protected virtual void RegisterSingleton<T>(
        IServiceCollection services,
        Func<IServiceProvider, T> factory
    )
        where T : class => services.AddSingleton(factory);

    /// <summary>执行单例服务注册，可在派生类中重写</summary>
    protected virtual void RegisterSingleton<TService, TImplementation>(
        IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService => services.AddSingleton<TService>(factory);

    /// <summary>执行延迟创建的单例自注册，可在派生类中重写</summary>
    protected virtual void RegisterLazySingleton<T>(
        IServiceCollection services,
        Func<IServiceProvider, T> factory
    )
        where T : class => services.AddSingleton(factory);

    /// <summary>执行延迟创建的单例服务注册，可在派生类中重写</summary>
    protected virtual void RegisterLazySingleton<TService, TImplementation>(
        IServiceCollection services,
        Func<IServiceProvider, TImplementation> factory
    )
        where TService : class
        where TImplementation : class, TService => services.AddSingleton<TService>(factory);
}
#pragma warning restore S1186, S2326
