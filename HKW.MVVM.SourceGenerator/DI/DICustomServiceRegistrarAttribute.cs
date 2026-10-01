namespace HKW.MVVM.SourceGenerator;

/// <summary>自定义服务注册方式</summary>
public enum DIServiceRegistration
{
    /// <summary>
    /// 瞬态
    /// </summary>
    Normal,

    /// <summary>
    /// 作用域
    /// </summary>
    Scoped,

    /// <summary>
    /// 单例
    /// </summary>
    Singleton,

    /// <summary>
    /// 延迟单例
    /// </summary>
    LazySingleton,
}

/// <summary>
/// 标记在 <see cref="DIConfigurationBase.Configure"/> 中调用的自定义服务注册方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DICustomServiceRegistrarAttribute : Attribute
{
    /// <summary>将指定泛型参数注册为瞬态服务</summary>
    /// <param name="GenericNames">泛型参数名称</param>
    public DICustomServiceRegistrarAttribute(params string[] GenericNames)
    {
        this.GenericNames = GenericNames;
        this.Registrations = Array.Empty<DIServiceRegistration>();
    }

    /// <summary>为每个泛型参数指定注册方式</summary>
    /// <param name="GenericNames">泛型参数名称</param>
    /// <param name="Registrations">泛型参数名称与注册方式</param>
    public DICustomServiceRegistrarAttribute(
        string[] GenericNames,
        DIServiceRegistration[] Registrations
    )
    {
        this.GenericNames = GenericNames;
        this.Registrations = Registrations;
    }

    /// <summary>
    /// 泛型名称
    /// </summary>
    public string[] GenericNames { get; }

    /// <summary>
    /// 泛型注册方式
    /// </summary>
    public DIServiceRegistration[] Registrations { get; }
}
