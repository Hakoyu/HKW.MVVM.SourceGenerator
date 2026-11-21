using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

/// <summary>
/// 生成器内部镜像, 避免分析器程序集在编译期加载运行时 DLL
/// </summary>
internal enum DIServiceRegistration
{
    Normal,
    Scoped,
    Singleton,
    LazySingleton,
}

/// <summary>
/// 生成器内部镜像, 避免分析器程序集在编译期加载运行时 DLL
/// </summary>
internal enum NotifyPropertyChangeFromCacheMode
{
    Disable,
    Enable,
    OnFirstChange,
}

internal static class NativeExtensions
{
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
}
