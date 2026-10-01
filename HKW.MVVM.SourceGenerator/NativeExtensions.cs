using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

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
