using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace HKW.MVVM.SourceGenerator;

[Generator]
internal sealed class DIGenerator : IIncrementalGenerator
{
    private const string RegistrationsTypeName = "HKW.MVVM.SourceGenerator.DIConfigurationBase";
    private const string ConfigurationAttributeName =
        "HKW.MVVM.SourceGenerator.DIConfigurationAttribute";
    private const string ConstructorAttributeName =
        "HKW.MVVM.SourceGenerator.DIConstructorAttribute";
    private const string PropertyAttributeName = "HKW.MVVM.SourceGenerator.DIPropertyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configurations = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                ConfigurationAttributeName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, _) =>
                    new ConfigurationInfo(
                        (INamedTypeSymbol)attributeContext.TargetSymbol,
                        (ClassDeclarationSyntax)attributeContext.TargetNode
                    )
            )
            .Collect();
        var registrations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntaxContext, _) => GetRegistration(syntaxContext)
            )
            .Where(static registration => registration is not null)
            .Select(static (registration, _) => registration!)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(configurations).Combine(registrations),
            static (productionContext, input) =>
                Generate(productionContext, input.Left.Left, input.Left.Right, input.Right)
        );
    }

    private static Registration? GetRegistration(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return null;
        if (method.ContainingType.ToDisplayString() != RegistrationsTypeName)
            return null;
        if (method.Parameters.Length != 0 || method.TypeArguments.Length is < 1 or > 2)
            return null;
        if (
            context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart)
            is not ISymbol enclosingSymbol
        )
            return null;

        var lifetime = method.Name switch
        {
            "Register" => ServiceLifetime.Transient,
            "RegisterScoped" => ServiceLifetime.Scoped,
            "RegisterSingleton" or "RegisterLazySingleton" => ServiceLifetime.Singleton,
            _ => ServiceLifetime.None,
        };
        if (lifetime == ServiceLifetime.None)
            return null;

        return new Registration(
            enclosingSymbol.ContainingType,
            method.TypeArguments[0],
            method.TypeArguments.Length == 1 ? method.TypeArguments[0] : method.TypeArguments[1],
            lifetime,
            method.Name,
            method.TypeArguments.Length == 2,
            invocation.GetLocation()
        );
    }

    private static void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ConfigurationInfo> configurations,
        ImmutableArray<Registration> registrations
    )
    {
        var configurationSymbols = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var configuration in configurations)
            configurationSymbols.Add(configuration.Symbol);
        foreach (
            var registration in registrations.Where(registration =>
                registration.ContainingType is null
                || configurationSymbols.Contains(registration.ContainingType) is false
            )
        )
        {
            var diagnostic = Diagnostic.Create(
                DIDescriptors.RegistrationOutsideConfiguration,
                registration.Location
            );
            context.ReportDiagnostic(diagnostic);
        }

        var generatedConfigurations = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var configuration in configurations)
        {
            if (generatedConfigurations.Add(configuration.Symbol) is false)
                continue;
            if (IsValidConfiguration(configuration) is false)
            {
                var diagnostic = Diagnostic.Create(
                    DIDescriptors.InvalidConfigurationType,
                    configuration.Syntax.Identifier.GetLocation(),
                    configuration.Symbol.ToDisplayString()
                );
                context.ReportDiagnostic(diagnostic);
                continue;
            }

            AddConfigurationSource(
                context,
                compilation,
                configuration,
                registrations
                    .Where(registration =>
                        SymbolEqualityComparer.Default.Equals(
                            registration.ContainingType,
                            configuration.Symbol
                        )
                    )
                    .OrderBy(registration => registration.Location.SourceTree?.FilePath)
                    .ThenBy(registration => registration.Location.SourceSpan.Start)
            );
        }
    }

    private static bool IsValidConfiguration(ConfigurationInfo configuration) =>
        configuration.Symbol.IsStatic is false
        && configuration.Symbol.IsAbstract is false
        && configuration.Symbol.Arity == 0
        && configuration.Symbol.ContainingType is null
        && configuration.Symbol.BaseType?.ToDisplayString() == RegistrationsTypeName
        && configuration.Symbol.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0
            && constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal
        )
        && configuration.Syntax.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static void AppendRegistration(
        SourceProductionContext context,
        Compilation compilation,
        IndentedTextWriter writer,
        Registration registration
    )
    {
        if (TryCreateFactory(context, compilation, registration, out var factory) is false)
            return;

        writer.Write(registration.MethodName);
        writer.Write('<');
        writer.Write(registration.ServiceType.GetFullName());
        if (registration.IsServiceMapping)
        {
            writer.Write(", ");
            writer.Write(registration.ImplementationType.GetFullName());
        }
        writer.WriteLine(">(services, serviceProvider =>");
        writer.WriteLine("{");
        writer.Indent++;
        writer.Write("return ");
        writer.Write(factory);
        writer.WriteLine(";");
        writer.Indent--;
        writer.WriteLine("});");
    }

    private static void AddConfigurationSource(
        SourceProductionContext context,
        Compilation compilation,
        ConfigurationInfo configuration,
        IEnumerable<Registration> registrations
    )
    {
        var stringStream = new StringWriter();
        var writer = new IndentedTextWriter(stringStream);
        var namespaceName = configuration.Symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : configuration.Symbol.ContainingNamespace.ToDisplayString();
        var accessibility =
            configuration.Symbol.DeclaredAccessibility == Accessibility.Public
                ? "public"
                : "internal";

        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine("#nullable enable");
        if (namespaceName is not null)
        {
            writer.WriteLine($"namespace {namespaceName}");
            writer.WriteLine("{");
            writer.Indent++;
        }
        writer.WriteLine($"{accessibility} partial class {configuration.Symbol.Name}");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("/// <summary>此配置的单例实例</summary>");
        writer.WriteLine($"public static {configuration.Symbol.Name} Instance {{ get; }} = new();");
        writer.WriteLine();
        writer.WriteLine("/// <summary>将此配置中的全部源生成注册应用到指定服务集合</summary>");
        writer.WriteLine(
            "public override global::Microsoft.Extensions.DependencyInjection.IServiceCollection Build("
        );
        writer.Indent++;
        writer.WriteLine(
            "global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)"
        );
        writer.Indent--;
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if (services is null)");
        writer.Indent++;
        writer.WriteLine("throw new global::System.ArgumentNullException(nameof(services));");
        writer.Indent--;
        writer.WriteLine("Configure(services);");
        foreach (var registration in registrations)
            AppendRegistration(context, compilation, writer, registration);
        writer.WriteLine("return services;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
        if (namespaceName is not null)
        {
            writer.Indent--;
            writer.WriteLine("}");
        }

        context.AddSource(
            $"{SanitizeHintName(configuration.Symbol.ToDisplayString())}.DI.g.cs",
            stringStream.ToString()
        );
    }

    private static bool TryCreateFactory(
        SourceProductionContext context,
        Compilation compilation,
        Registration registration,
        out string factory
    )
    {
        factory = string.Empty;
        if (
            registration.ImplementationType is not INamedTypeSymbol implementationType
            || implementationType.TypeKind != TypeKind.Class
            || implementationType.IsAbstract
            || implementationType.IsUnboundGenericType
        )
        {
            var diagnostic = Diagnostic.Create(
                DIDescriptors.InvalidImplementationType,
                registration.Location,
                registration.ImplementationType.GetName()
            );
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        if (
            compilation
                .ClassifyCommonConversion(implementationType, registration.ServiceType)
                .IsImplicit
            is false
        )
        {
            var diagnostic = Diagnostic.Create(
                DIDescriptors.IncompatibleServiceType,
                registration.Location,
                implementationType.GetName(),
                registration.ServiceType.GetName()
            );
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        var constructors = implementationType.InstanceConstructors;
        var markedConstructors = constructors
            .Where(static constructor => HasAttribute(constructor, ConstructorAttributeName))
            .ToImmutableArray();
        IMethodSymbol constructor;
        if (constructors.Length == 1 && markedConstructors.Length <= 1)
            constructor = constructors[0];
        else if (markedConstructors.Length == 1)
            constructor = markedConstructors[0];
        else
        {
            var diagnostic = Diagnostic.Create(
                DIDescriptors.AmbiguousConstructor,
                registration.Location,
                implementationType.GetName()
            );
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        if (constructor.DeclaredAccessibility < Accessibility.Internal)
        {
            var diagnostic = Diagnostic.Create(
                DIDescriptors.InaccessibleConstructor,
                constructor.Locations.FirstOrDefault() ?? registration.Location,
                implementationType.GetName()
            );
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        var arguments = string.Join(
            ", ",
            constructor.Parameters.Select(parameter => Resolve(parameter.Type))
        );
        var properties = GetInjectedProperties(compilation, implementationType);
        foreach (var property in properties)
        {
            if (
                property.IsStatic
                || property.IsIndexer
                || property.SetMethod is null
                || property.SetMethod.DeclaredAccessibility < Accessibility.Internal
            )
            {
                var diagnostic = Diagnostic.Create(
                    DIDescriptors.InvalidInjectedProperty,
                    property.Locations.FirstOrDefault() ?? registration.Location,
                    property.Name,
                    implementationType.GetName()
                );
                context.ReportDiagnostic(diagnostic);
                return false;
            }
        }

        var builder = new StringBuilder()
            .Append("new ")
            .Append(implementationType.GetFullName())
            .Append('(')
            .Append(arguments)
            .Append(')');
        if (properties.Count > 0)
        {
            builder.Append(" { ");
            foreach (var property in properties)
            {
                builder
                    .Append(EscapeIdentifier(property.Name))
                    .Append(" = ")
                    .Append(Resolve(property.Type))
                    .Append(", ");
            }
            builder.Append('}');
        }
        factory = builder.ToString();
        return true;
    }

    private static List<IPropertySymbol> GetInjectedProperties(
        Compilation compilation,
        INamedTypeSymbol implementationType
    )
    {
        var properties = new List<IPropertySymbol>();
        for (var type = implementationType; type is not null; type = type.BaseType)
        {
            if (type.ContainingAssembly.SymbolEquals(compilation.Assembly) is false)
                break;
            properties.AddRange(
                type.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(static property => HasAttribute(property, PropertyAttributeName))
            );
        }
        return properties;
    }

    private static string Resolve(ITypeSymbol type)
    {
        if (
            type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.TypeArguments.Length == 1
        )
        {
            var definition = namedType.OriginalDefinition.ToDisplayString();
            var argument = namedType.TypeArguments[0].GetFullName();
            if (definition == "System.Lazy<T>")
            {
                return $"new global::System.Lazy<{argument}>(() => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{argument}>(serviceProvider))";
            }
            if (definition == "System.Collections.Generic.IEnumerable<T>")
            {
                return $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<{argument}>(serviceProvider)";
            }
        }
        return $"global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<{type.GetFullName()}>(serviceProvider)";
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName) =>
        symbol
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToDisplayString() == attributeName);

    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static string SanitizeHintName(string name) =>
        new(name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private enum ServiceLifetime
    {
        None,
        Transient,
        Scoped,
        Singleton,
    }

    private sealed class ConfigurationInfo(INamedTypeSymbol symbol, ClassDeclarationSyntax syntax)
    {
        public INamedTypeSymbol Symbol { get; } = symbol;
        public ClassDeclarationSyntax Syntax { get; } = syntax;
    }

    private sealed class Registration(
        INamedTypeSymbol? containingType,
        ITypeSymbol serviceType,
        ITypeSymbol implementationType,
        ServiceLifetime lifetime,
        string methodName,
        bool isServiceMapping,
        Location location
    )
    {
        public INamedTypeSymbol? ContainingType { get; } = containingType;
        public ITypeSymbol ServiceType { get; } = serviceType;
        public ITypeSymbol ImplementationType { get; } = implementationType;
        public ServiceLifetime Lifetime { get; } = lifetime;
        public string MethodName { get; } = methodName;
        public bool IsServiceMapping { get; } = isServiceMapping;
        public Location Location { get; } = location;
    }
}
