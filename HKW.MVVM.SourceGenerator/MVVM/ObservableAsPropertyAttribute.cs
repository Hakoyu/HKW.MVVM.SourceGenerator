namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 观察属性
/// <para></para>
/// <para>
/// 示例:
/// <code><![CDATA[
/// partial class MyViewModel : ObservableObject
/// {
///     [ObservableAsProperty]
///     public string FirstName =>
///         this.WhenAnyValue(x => x.Name)
///             .Where(n => !string.IsNullOrWhiteSpace(n))
///             .Select(n => n.Split(' ')[0])
///             .ToProperty(this, nameof(FirstName))
///             .Value;
///
///     protected void InitializeReactiveObject() { }
/// }
/// ]]></code>
/// </para>
/// 这样就会生成代码
/// <code><![CDATA[
/// partial class MyViewModel : ObservableObject
/// {
///     [ObservableAsProperty]
///     public string FirstName => _firstName
///
///     private ObservableAsPropertyHelper<string> _firstNameOAPH;
///
///     protected void InitializeReactiveObject()
///     {
///        _fullName =
///            this.WhenAnyValue(x => x.Name)
///                .Where(n => !string.IsNullOrWhiteSpace(n))
///                .Select(n => n.Split(' ')[0])
///                .ToProperty(this, nameof(FirstName));
///     }
/// }
/// ]]></code></summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ObservableAsPropertyAttribute : Attribute { }
