using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal static class DIDescriptors
{
    private const string Category = "HKW.MVVM.SourceGenerator.DependencyInjection";

    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        "DI001",
        "Dependency injection constructor is ambiguous",
        "Type '{0}' has multiple constructors; mark exactly one with [DependencyInjectionConstructor]",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidInjectedProperty = new(
        "DI002",
        "Dependency injection property is not writable",
        "Property '{0}' on type '{1}' must be an instance property with an internal or public setter",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InaccessibleConstructor = new(
        "DI003",
        "Dependency injection constructor is inaccessible",
        "The selected constructor on type '{0}' must be internal or public",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidImplementationType = new(
        "DI004",
        "Dependency injection implementation is invalid",
        "Type '{0}' must be a non-abstract, closed class",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor IncompatibleServiceType = new(
        "DI005",
        "Dependency injection service and implementation are incompatible",
        "Implementation type '{0}' is not assignable to service type '{1}'",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidConfigurationType = new(
        "DI006",
        "Dependency injection configuration type is invalid",
        "Type '{0}' must be a top-level, non-generic, non-abstract partial class inheriting DIConfigurationBase with an accessible parameterless constructor",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor InvalidCustomServiceRegistrarGenericName = new(
        "DI007",
        "Dependency injection custom service registrar generic name is invalid",
        "Generic name '{0}' must be a type parameter of method '{1}'",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor CustomServiceRegistrarRegistrationCountMismatch =
        new(
            "DI008",
            "Dependency injection custom service registrar registration count is invalid",
            "Custom service registrar method '{0}' specifies {1} generic names but {2} registration modes",
            Category,
            DiagnosticSeverity.Error,
            true
        );

    public static readonly DiagnosticDescriptor DuplicateRegistration = new(
        "DI009",
        "Dependency injection registration is duplicated",
        "Service type '{0}' with implementation type '{1}' is registered more than once using '{2}'",
        Category,
        DiagnosticSeverity.Warning,
        true
    );

    public static readonly DiagnosticDescriptor CircularDependency = new(
        "DI010",
        "Dependency injection graph contains a cycle",
        "A circular dependency was detected: {0}",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor CaptiveScopedDependency = new(
        "DI011",
        "Singleton captures a scoped service",
        "Singleton service '{0}' cannot consume scoped service '{1}' through dependency path: {2}",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor PossiblyMissingDependency = new(
        "DI012",
        "Dependency may not be registered",
        "Service '{0}' requires '{1}', which is not present in the statically known registration graph and must be supplied by Configure",
        Category,
        DiagnosticSeverity.Warning,
        true
    );
}
