namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 指定源生成依赖注入注册所使用的构造函数
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class DIConstructorAttribute : Attribute;
