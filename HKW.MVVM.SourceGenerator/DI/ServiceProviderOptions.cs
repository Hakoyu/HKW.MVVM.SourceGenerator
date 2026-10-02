using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 配置轻量 <see cref="DIServiceProvider"/> 的验证行为
/// </summary>
public sealed class ServiceProviderOptions
{
    /// <summary>
    /// 获取或设置是否验证作用域服务的解析位置
    /// </summary>
    public bool ValidateScopes { get; set; }

    /// <summary>
    /// 获取或设置是否在构建 Provider 时验证已知依赖图
    /// </summary>
    public bool ValidateOnBuild { get; set; }
}

/// <summary>
/// 供依赖注入源生成代码描述注册依赖
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DIServiceRegistrationMetadata
{
    /// <summary>
    /// 初始化注册元数据
    /// </summary>
    public DIServiceRegistrationMetadata(
        Type serviceType,
        ServiceLifetime lifetime,
        DIServiceDependencyMetadata[] dependencies,
        bool dependenciesUnknown = false
    )
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        Lifetime = lifetime;
        Dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        DependenciesUnknown = dependenciesUnknown;
    }

    /// <summary>
    /// 获取服务类型
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// 获取服务生命周期
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// 获取静态可知的依赖
    /// </summary>
    public IReadOnlyList<DIServiceDependencyMetadata> Dependencies { get; }

    /// <summary>
    /// 获取依赖是否包含无法静态分析的部分
    /// </summary>
    public bool DependenciesUnknown { get; }
}

/// <summary>
/// 供依赖注入源生成代码描述单个依赖
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DIServiceDependencyMetadata
{
    /// <summary>
    /// 初始化依赖元数据
    /// </summary>
    public DIServiceDependencyMetadata(Type serviceType, bool isEnumerable = false)
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        IsEnumerable = isEnumerable;
    }

    /// <summary>
    /// 获取依赖服务类型
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// 获取依赖是否按集合解析
    /// </summary>
    public bool IsEnumerable { get; }
}
