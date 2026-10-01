using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class NativeExtensions
{
    /// <summary>
    /// 含有特性
    /// </summary>
    /// <param name="symbol">符号类型</param>
    /// <param name="attributeTypeFullName">特性名称</param>
    /// <returns>特性数据</returns>
    public static bool HasAttribute(this ISymbol symbol, string attributeTypeFullName)
    {
        var isGlobal = attributeTypeFullName.IsGlobalName();
        return symbol
            .GetAttributes()
            .FirstOrDefault(x => x.AttributeClass!.GetFullName(isGlobal) == attributeTypeFullName)
            is not null;
    }

    public static string ToCode(this DIServiceRegistration registration)
    {
        return registration switch
        {
            DIServiceRegistration.Normal => "Register",
            DIServiceRegistration.Scoped => "RegisterScoped",
            DIServiceRegistration.Singleton => "RegisterSingleton",
            DIServiceRegistration.LazySingleton => "RegisterLazySingleton",
            _ => string.Empty,
        };
    }

    public static TValue[]? GetParams<TValue>(this AttributeInfo attributeInfo, string paramName)
    {
        attributeInfo.TryGetValue(paramName, out var value);
        return value.Values is null ? null : value.Values.Cast<TValue>().ToArray<TValue>();
    }
}
