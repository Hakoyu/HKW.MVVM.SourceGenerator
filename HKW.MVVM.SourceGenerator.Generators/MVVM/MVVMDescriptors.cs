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
    public static readonly DiagnosticDescriptor AttributePropertyNotHaveSetMethod = new(
        id: "MVVM0002",
        title: "Property not have SetMethod",
        messageFormat: "Attribute '{0}' is not valid for property without SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
    public static readonly DiagnosticDescriptor AttributePropertyHasSetMethod = new(
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
    public static readonly DiagnosticDescriptor RelayCommandCanExecutePropertyNotExist = new(
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
        DiagnosticSeverity.Warning,
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

    public static readonly DiagnosticDescriptor NotifyPropertyChangeForTargetHasSetMethod = new(
        id: "MVVM0008",
        title: "Property has SetMethod",
        messageFormat: "Attribute 'NotifyPropertyChangeFor' is not valid for target property '{0}' with SetMethod",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RelayCommandCanExecutePropertyNotObservable = new(
        id: "MVVM0009",
        title: "Command can execute property is not observable",
        messageFormat: "Command can execute property '{0}' is not observable property, add 'ObservableProperty' or 'NotifyProperty..' attribute to it",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotifyCanExecuteChangedForWarning = new(
        id: "MVVM0010",
        title: "Notify can execute changed for warning",
        messageFormat: "The NotifyCanExecuteChangedForAttribute no longer takes effect, if current property is observable property, NotifyCanExecuteChanged is auto triggered after the property changed.",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ObservableAsPropertyStructuralError = new(
        id: "MVVM0009",
        title: "ObservableAsProperty set method structural error",
        messageFormat: "ObservableAsProperty set method structural error, structural must is 'this.WhenAnyValue(...).DoSome().ToProperty(...).Value'",
        category: Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
