using System.Collections;
using System.Text;
using Microsoft.CodeAnalysis;
using Mono.Cecil;

namespace HKW.MVVM.Fody;

internal static class NativeExtensions
{
    /// <summary>
    /// 获取真实名称, 去除类名中"`1"等内容
    /// </summary>
    /// <param name="name">名称</param>
    /// <returns>真实名称</returns>
    public static string GetRealName(this string name)
    {
        var genericMarkerIndex = name.IndexOf('`');
        return genericMarkerIndex < 0 ? name : name.Substring(0, genericMarkerIndex);
    }

    /// <summary>
    /// 首字母小写
    /// </summary>
    /// <param name="str">字符串</param>
    /// <returns>首字母为小写的字符串</returns>
    public static string FirstLetterToLower(this string str)
    {
        if (string.IsNullOrWhiteSpace(str) || char.IsLower(str, 0))
            return str;
        var array = str.ToCharArray();
        array[0] = char.ToLowerInvariant(array[0]);
        return new string(array);
    }

    /// <summary>
    /// 获取特性构造参数
    /// </summary>
    /// <param name="customAttribute">特性</param>
    /// <returns>参数字典</returns>
    public static AttributeParamDictionary GetParams(this CustomAttribute? customAttribute)
    {
        if (customAttribute is null)
            return null!;
        return new AttributeParamDictionary(customAttribute);
    }

    /// <summary>
    /// 获取特性构造参数
    /// </summary>
    /// <param name="customAttribute">特性</param>
    /// <returns>参数字典</returns>
    public static bool TryGetParams(
        this CustomAttribute? customAttribute,
        out AttributeParamDictionary attributeParams
    )
    {
        attributeParams = null!;
        if (customAttribute is null)
            return false;
        attributeParams = new AttributeParamDictionary(customAttribute);
        return true;
    }
}

/// <summary>
/// 特性参数字典(ParamName, ParamValue)
/// </summary>
public class AttributeParamDictionary : IDictionary<string, AttributeParam>
{
    private readonly Dictionary<string, AttributeParam> _dictionary = [];

    /// <inheritdoc/>
    /// <param name="attributeData">特性数据</param>
    public AttributeParamDictionary(CustomAttribute customAttribute)
    {
        // 分析构造,但ConstructorArguments重没有参数名称,所以从Resolve获取参数名称
        foreach (
            var (argument, parameter) in customAttribute.ConstructorArguments.Zip(
                customAttribute.Constructor.Resolve().Parameters,
                (argument, parameter) => (argument, parameter)
            )
        )
        {
            _dictionary.Add(parameter.Name, new(argument));
        }
    }

    /// <summary>
    /// 尝试获取参数值
    /// </summary>
    /// <typeparam name="TValue">类型</typeparam>
    /// <param name="parameterName">参数名称</param>
    /// <param name="paramValue"></param>
    /// <returns>是否存在</returns>
    public bool TryGetParam<TValue>(string parameterName, out TValue paramValue)
    {
        if (typeof(TValue).IsArray)
            throw new NotSupportedException();
        var r = _dictionary.TryGetValue(parameterName, out var value);
        paramValue = r ? (TValue)value.Value! : default!;
        return r;
    }

    /// <summary>
    /// 尝试获取参数数组
    /// </summary>
    /// <typeparam name="TValue">类型</typeparam>
    /// <param name="parameterName">参数名称</param>
    /// <param name="parameterArray">参数数组</param>
    /// <returns>是否存在</returns>
    public bool TryGetParams<TValue>(string parameterName, out IEnumerable<TValue> parameterArray)
    {
        var r = _dictionary.TryGetValue(parameterName, out var value);
        parameterArray = r ? value.Values.Cast<TValue>() : default!;
        return r;
    }

    #region IDictionary
    /// <inheritdoc/>
    public AttributeParam this[string key]
    {
        get => _dictionary[key];
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public ICollection<string> Keys => _dictionary.Keys;

    /// <inheritdoc/>
    public ICollection<AttributeParam> Values => _dictionary.Values;

    /// <inheritdoc/>
    public int Count => _dictionary.Count;

    /// <inheritdoc/>
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    void IDictionary<string, AttributeParam>.Add(string key, AttributeParam value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, AttributeParam>>.Add(
        KeyValuePair<string, AttributeParam> item
    )
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    bool IDictionary<string, AttributeParam>.Remove(string key)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    bool ICollection<KeyValuePair<string, AttributeParam>>.Remove(
        KeyValuePair<string, AttributeParam> item
    )
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    void ICollection<KeyValuePair<string, AttributeParam>>.Clear()
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<string, AttributeParam> item)
    {
        return ((ICollection<KeyValuePair<string, AttributeParam>>)_dictionary).Contains(item);
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key)
    {
        return _dictionary.ContainsKey(key);
    }

    /// <inheritdoc/>
    public void CopyTo(KeyValuePair<string, AttributeParam>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, AttributeParam>>)_dictionary).CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public bool TryGetValue(string key, out AttributeParam value)
    {
        return _dictionary.TryGetValue(key, out value);
    }

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<string, AttributeParam>> GetEnumerator()
    {
        return _dictionary.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
    #endregion
}

/// <summary>
/// 特性参数值
/// </summary>
public readonly struct AttributeParam
{
    /// <inheritdoc/>
    /// <param name="argument">特性参数</param>
    public AttributeParam(CustomAttributeArgument argument)
    {
        if (argument.Value is CustomAttributeArgument[] args)
            Values = args.Select(x => x.Value).ToArray();
        else
            Value = argument.Value;
    }

    /// <summary>
    /// 参数值
    /// </summary>
    public object? Value { get; } = null;

    /// <summary>
    /// 参数值数组, 用于 paras 类型参数
    /// </summary>
    public object?[]? Values { get; } = null;
}
