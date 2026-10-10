using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 方法属性信息
/// </summary>
internal sealed class NotifyPropertyChangeFromInfo
{
    /// <summary>
    /// </summary>
    /// <param name="property">属性</param>
    /// <param name="getMethod">方法</param>
    /// <param name="propertyNames">属性名</param>
    public NotifyPropertyChangeFromInfo(
        IPropertySymbol property,
        string getMethod,
        HashSet<string> propertyNames
    )
    {
        Property = property;
        GetMethod = getMethod;
        PropertyNames = propertyNames;
        ChangingMethodName = $"Notify{property.Name}Changing";
        ChangedMethodName = $"Notify{property.Name}Changed";
    }

    public IPropertySymbol Property { get; set; }

    public NotifyPropertyChangeFromCacheMode CacheMode { get; set; }
    public string GetMethod { get; set; }

    public HashSet<string> PropertyNames { get; set; }

    public string ChangingMethodName { get; set; }
    public string ChangedMethodName { get; set; }
}
