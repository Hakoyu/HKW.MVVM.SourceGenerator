namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 标记包含源生成依赖注入注册的静态分部配置类。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DIConfigurationAttribute : Attribute;
