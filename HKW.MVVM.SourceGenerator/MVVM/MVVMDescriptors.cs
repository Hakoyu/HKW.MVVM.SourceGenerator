using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class MVVMDescriptors
{
    public const string Category = "HKW.MVVM.SourceGenerator";

    public static readonly DiagnosticDescriptor NotPartialClass = new(
        id: "HKWMVVM0001",
        title: "Not partial class",
        messageFormat: "This class implemented ObservableObject but it is not partial class, place add partial key word",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor PropertyNotHaveSetMethod = new(
        id: "HKWMVVM0002",
        title: "Property not have SetMethod",
        messageFormat: "Attribute [{0}] is not valid for property without SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor PropertyHasSetMethod = new(
        id: "HKWMVVM0003",
        title: "Property has SetMethod",
        messageFormat: "Attribute [{0}] is not valid for property with SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor RelayCommandParametersGreaterThan1 = new(
        id: "HKWMVVM0004",
        title: "Parameters greater than 1",
        messageFormat: "Relay command parameters greater than 1",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
