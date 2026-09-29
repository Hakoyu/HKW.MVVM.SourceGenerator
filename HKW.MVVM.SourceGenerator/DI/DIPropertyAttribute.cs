namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 标记需要由源生成依赖注入进行注入的属性。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DIPropertyAttribute : Attribute;
