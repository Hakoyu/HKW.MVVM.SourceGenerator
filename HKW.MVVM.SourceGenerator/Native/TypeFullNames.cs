using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class TypeFullNames
{
    public const string ObservablePropertyAttribute =
        "global::CommunityToolkit.Mvvm.ComponentModel.ObservablePropertyAttribute";
    public const string RelayCommandAttribute =
        "global::CommunityToolkit.Mvvm.Input.RelayCommandAttribute";

    public const string NotifyPropertyChangeForAttribute =
        "global::CommunityToolkit.Mvvm.ComponentModel.ObservableObjectAttribute";
    public const string ObservableObject =
        "global::CommunityToolkit.Mvvm.ComponentModel.ObservableObject";
    public static string NotifyPropertyChangeFrom { get; } =
        typeof(NotifyPropertyChangeFromAttribute).GetGlobalFullName();

    public static string ObservableAsPropertyAttribute { get; } =
        typeof(ObservableAsPropertyAttribute).GetGlobalFullName();
}
