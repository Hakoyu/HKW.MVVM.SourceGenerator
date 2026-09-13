using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 方法属性信息
/// </summary>
/// <param name="property">属性</param>
/// <param name="getMethod">方法</param>
/// <param name="params">参数</param>
internal sealed class NotifyPropertyChangeFromInfo(
    IPropertySymbol property,
    string getMethod,
    string[] @params
)
{
    public IPropertySymbol Property { get; set; } = property;

    public NotifyPropertyChangeFromCacheMode CacheMode { get; set; }
    public string GetMethod { get; set; } = getMethod;

    public string[] Params { get; set; } = @params;

    public string ChangingMethodName { get; set; } = $"Notify{property.Name}Changing";
    public string ChangedMethodName { get; set; } = $"Notify{property.Name}Changed";
}
