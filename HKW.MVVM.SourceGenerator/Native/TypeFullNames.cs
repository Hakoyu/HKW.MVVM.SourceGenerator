using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class TypeFullNames
{
    public const string ObservablePropertyAttribute =
        "CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
    public const string RelayCommandAttribute = "CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

    public const string NotifyPropertyChangeForAttribute =
        "CommunityToolkit.Mvvm.ComponentModel.ObservableObjectAttribute";
    public static string NotifyPropertyChangeFrom { get; } =
        typeof(NotifyPropertyChangeFromAttribute).FullName;
    public const string ObservableObject = "CommunityToolkit.Mvvm.ComponentModel.ObservableObject";

    public static bool InheritedFromX(
        this ITypeSymbol typeSymbol,
        string baseTypeFullName,
        SymbolDisplayFormat? symbolDisplayFormat = null
    )
    {
        var currentType = typeSymbol;
        while (currentType != null)
        {
            var typeName = symbolDisplayFormat is null
                ? currentType.ToString()
                : currentType.ToDisplayString(symbolDisplayFormat);
            if (typeName == baseTypeFullName)
                return true;
            currentType = currentType.BaseType;
        }
        return false;
    }
}
