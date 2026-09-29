using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class DIDescriptors
{
    private const string Category = "HKW.MVVM.SourceGenerator.DependencyInjection";

    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "HKWDI001",
        "Dependency injection constructor is ambiguous",
        "Type '{0}' has multiple constructors; mark exactly one with [DependencyInjectionConstructor]",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidInjectedProperty = new(
        "HKWDI002",
        "Dependency injection property is not writable",
        "Property '{0}' on type '{1}' must be an instance property with an internal or public setter",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InaccessibleConstructor = new(
        "HKWDI003",
        "Dependency injection constructor is inaccessible",
        "The selected constructor on type '{0}' must be internal or public",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidImplementationType = new(
        "HKWDI004",
        "Dependency injection implementation is invalid",
        "Type '{0}' must be a non-abstract, closed class",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor IncompatibleServiceType = new(
        "HKWDI005",
        "Dependency injection service and implementation are incompatible",
        "Implementation type '{0}' is not assignable to service type '{1}'",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor RegistrationOutsideConfiguration = new(
        "HKWDI006",
        "Dependency injection registration is outside a configuration",
        "Dependency injection registration must be declared inside a class marked with [DIConfiguration]",
        Category,
        DiagnosticSeverity.Warning,
        true
    );

    public static readonly DiagnosticDescriptor InvalidConfigurationType = new(
        "HKWDI007",
        "Dependency injection configuration type is invalid",
        "Type '{0}' must be a top-level, non-generic, static partial class",
        Category,
        DiagnosticSeverity.Error,
        true
    );
}
