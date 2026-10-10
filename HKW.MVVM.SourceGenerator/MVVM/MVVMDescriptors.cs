using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class MVVMDescriptors
{
    public const string Category = "HKW.MVVM.SourceGenerator";

    public static readonly DiagnosticDescriptor NotPartialClass = new(
        id: "MVVM0001",
        title: "Not partial class",
        messageFormat: "This class implemented ObservableObject but it is not partial class, place add partial key word",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor PropertyNotHaveSetMethod = new(
        id: "MVVM0002",
        title: "Property not have SetMethod",
        messageFormat: "Attribute '{0}' is not valid for property without SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor PropertyHasSetMethod = new(
        id: "MVVM0003",
        title: "Property has SetMethod",
        messageFormat: "Attribute '{0}' is not valid for property with SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor RelayCommandParametersGreaterThan1 = new(
        id: "MVVM0004",
        title: "Command parameters greater than 1",
        messageFormat: "Relay command parameters greater than 1",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor RelayCommandCommandCanExecutePropertyNotExist = new(
        id: "MVVM0005",
        title: "Command can execute property not exist",
        messageFormat: "Relay command can execute property '{0}' not exist",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor NotifyPropertyChangeFromDuplicateProperty = new(
        id: "MVVM0006",
        title: "Duplicate notify property change source property",
        messageFormat: "Notify property change source property '{0}' is duplicated",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor NotifyPropertyChangeFromPropertyNotExist = new(
        id: "MVVM0007",
        title: "Notify property change source property not exist",
        messageFormat: "Notify property change source property '{0}' does not exist",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
