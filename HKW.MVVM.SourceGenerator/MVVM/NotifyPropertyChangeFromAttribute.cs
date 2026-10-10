namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 从目标属性通知当前属性改变
/// <para>
/// 示例1:
/// <code><![CDATA[
/// [NotifyPropertyChangeFrom(nameof(Name)]
/// public bool IsValidName => string.IsNullOrWhiteSpace(Name) is false;
/// ]]></code>
/// 生成代码:
/// <code><![CDATA[
/// private void SetPropertyName(ref string backingField, string newValue)
/// {
///     ...
///     OnPropertyChanging("IsValidName");
///     // backingField = newValue
///     ...
///     OnPropertyChanged("IsValidName");
/// }
/// ]]></code>
/// </para>
/// <para>
/// 示例2:
/// <code><![CDATA[
/// [NotifyPropertyChangeFrom(NotifyPropertyChangeFromCacheMode.Enable, nameof(Name)]
/// public bool IsValidName => string.IsNullOrWhiteSpace(Name) is false;
/// ]]></code>
/// 生成代码:
/// <code><![CDATA[
/// private bool _isValidName;
/// [NotifyPropertyChangeFrom(nameof(Name)]
/// public bool IsValidName => _isValidName;
///
/// private bool GetIsValidName()
/// {
///     return string.IsNullOrWhiteSpace(Name) is false;
/// }
/// protected void NotifyIsValidNameChanging()
/// {
///     OnPropertyChanging("IsValidName");
/// }
/// protected void NotifyIsValidNameChanged()
/// {
///     _isValidName = GetIsValidName();
///     OnPropertyChanged("IsValidName");
/// }
/// private void SetPropertyName(ref string backingField, string newValue)
/// {
///     ...
///     NotifyIsValidNameChanging();
///     // backingField = newValue
///     ...
///     NotifyIsValidNameChanged();
/// }
/// ]]></code>
/// </para>
/// </summary>
/// <remarks>
/// <see cref="CacheMode"/> 启用时会生成一个字段来提高性能
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed partial class NotifyPropertyChangeFromAttribute : Attribute
{
    ///<inheritdoc/>
    /// <param name="propertyName">属性名称</param>
    /// <param name="propertyNames">属性名称</param>
    public NotifyPropertyChangeFromAttribute(string propertyName, params string[] propertyNames)
    {
        this.PropertyNames = [propertyName, .. propertyNames];
    }

    ///<inheritdoc/>
    /// <param name="CacheMode">启用缓存</param>
    /// <param name="propertyName">属性名称</param>
    /// <param name="propertyNames">属性名称</param>
    public NotifyPropertyChangeFromAttribute(
        NotifyPropertyChangeFromCacheMode CacheMode,
        string propertyName,
        params string[] propertyNames
    )
    {
        this.PropertyNames = [propertyName, .. propertyNames];
        this.CacheMode = CacheMode;
    }

    /// <summary>
    /// 属性名称
    /// </summary>
    public string[] PropertyNames { get; }

    /// <summary>
    /// 缓存模式
    /// </summary>
    public NotifyPropertyChangeFromCacheMode CacheMode { get; }
}

/// <summary>
/// 缓存模式
/// </summary>
public enum NotifyPropertyChangeFromCacheMode
{
    /// <summary>
    /// 禁用
    /// </summary>
    Disable,

    /// <summary>
    /// 启用, 并在对象初始化时缓存
    /// </summary>
    Enable,

    /// <summary>
    /// 首次关联属性变更时才计算并缓存。
    /// </summary>
    OnFirstChange,
}
