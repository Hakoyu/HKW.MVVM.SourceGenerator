namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 标记继承 <see cref="DIConfigurationBase"/> 的分部配置类。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DIConfigurationAttribute : Attribute;
