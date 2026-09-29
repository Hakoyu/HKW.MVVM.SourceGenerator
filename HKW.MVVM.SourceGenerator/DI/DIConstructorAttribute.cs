namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// Selects the constructor used by generated dependency injection registrations.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class DIConstructorAttribute : Attribute;
