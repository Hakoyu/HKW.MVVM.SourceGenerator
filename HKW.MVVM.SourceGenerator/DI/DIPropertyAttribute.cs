namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// Marks a property for injection by generated dependency injection registrations.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DIPropertyAttribute : Attribute;
