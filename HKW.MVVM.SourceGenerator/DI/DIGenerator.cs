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
    private static string RegistrationsTypeName { get; } =
        typeof(DIConfigurationBase).GetFullName();
    private static string CustomRegistrarAttributeName { get; } =
        typeof(DICustomServiceRegistrarAttribute).GetFullName();
    private static string ConstructorAttributeName { get; } =
        typeof(DIConstructorAttribute).GetFullName();
    private static string PropertyAttributeName { get; } =
        typeof(DIPropertyAttribute).GetFullName();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configurations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) => GetConfiguration(syntaxContext)
            )
            .Where(static configuration => configuration is not null)
            .Select(static (configuration, _) => configuration!)
            .Collect();
        var registrations = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntaxContext, _) => GetRegistrations(syntaxContext)
            )
            .Where(static registrations => registrations.IsDefaultOrEmpty is false)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(configurations).Combine(registrations),
            static (productionContext, input) =>
                Generate(
                    productionContext,
                    input.Left.Left,
                    input.Left.Right,
                    input.Right.SelectMany(static registrations => registrations).ToImmutableArray()
                )
        );
    }

    private static ConfigurationInfo? GetConfiguration(GeneratorSyntaxContext context)
    {
        var syntax = (ClassDeclarationSyntax)context.Node;
        if (
            context.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol
            || symbol.BaseType?.GetFullName() != RegistrationsTypeName
        )
            return null;

        return new ConfigurationInfo(symbol, syntax);
    }

    private static ImmutableArray<Registration> GetRegistrations(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (
            context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart)
                is not IMethodSymbol enclosingMethod
            || IsConfigureMethod(enclosingMethod) is false
        )
            return [];

        if (
            method.ContainingType.ToDisplayString() == RegistrationsTypeName
            && method.Parameters.Length == 0
            && method.TypeArguments.Length is >= 1 and <= 2
        )
        {
            return
            [
                new Registration(
                    enclosingMethod.ContainingType,
                    method.TypeArguments[0],
                    method.TypeArguments.Length == 1
                        ? method.TypeArguments[0]
                        : method.TypeArguments[1],
                    method.Name,
                    method.TypeArguments.Length == 2,
                    invocation.GetLocation()
                ),
            ];
        }

        var attributeInfo = method.GetFirstAttribute(CustomRegistrarAttributeName).GetInfo();
        if (attributeInfo is null)
            return [];

        var genericNameValues = attributeInfo
            .GetParams<string>(nameof(DICustomServiceRegistrarAttribute.GenericNames))
            .ToArray();
        var registrationValues = NativeExtensions.GetParams<DIServiceRegistration>(
            attributeInfo,
            nameof(DICustomServiceRegistrarAttribute.Registrations)
        );
        if (registrationValues is not null && registrationValues.Length != genericNameValues.Length)
        {
            //context.ReportDiagnostic(diagnostic);
            return [];
        }
        var registrations = ImmutableArray.CreateBuilder<Registration>();
        for (var index = 0; index < genericNameValues.Length; index++)
        {
            var genericName = genericNameValues[index];
            var genericParam = method.TypeParameters.FirstOrDefault(parameter =>
                parameter.Name == genericName
            );
            if (genericParam is null)
            {
                //var diagnostic = Diagnostic.Create(
                //    DIDescriptors.InvalidCustomServiceRegistrarGenericName,
                //    attributeInfo.Data.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                //        attributeInfo.Data.ApplicationSyntaxReference.Span
                //    ),
                //    genericParam?.GetName(),
                //    method.Name
                //);
                //context.ReportDiagnostic(diagnostic);
                continue;
            }
            var methodName = "Register";
            if (registrationValues is not null)
            {
                var registrationMode = registrationValues[index];
                methodName = registrationMode.ToCode();
                if (string.IsNullOrWhiteSpace(methodName))
                    continue;
            }
            var serviceType = method.TypeArguments[method.TypeParameters.IndexOf(genericParam)];
            registrations.Add(
                new Registration(
                    enclosingMethod.ContainingType,
                    serviceType,
                    serviceType,
                    methodName,
                    false,
                    invocation.GetLocation()
                )
            );
        }
        return registrations.ToImmutable();
    }

    private static bool IsConfigureMethod(IMethodSymbol method) =>
        method.Name == "Configure"
        && method.IsOverride
        && method.OverriddenMethod?.ContainingType.ToDisplayString() == RegistrationsTypeName;

    private static void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ConfigurationInfo> configurations,
        ImmutableArray<Registration> registrations
    )
    {
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
            && constructor.DeclaredAccessibility >= Accessibility.Internal
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
        writer.Write(registration.ServiceType.GetGlobalFullName());
        if (registration.IsServiceMapping)
        {
            writer.Write(", ");
            writer.Write(registration.ImplementationType.GetGlobalFullName());
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
        writer.WriteLine("/// <summary>延迟创建的配置单例实例</summary>");
        writer.WriteLine(GeneratorHelper.GeneratedCodeAttributeName);
        writer.WriteLine(GeneratorHelper.DebuggerBrowsableNeverAttributeName);
        writer.WriteLine(
            $"private static readonly global::System.Lazy<{configuration.Symbol.Name}> _instance = new(() => new());"
        );
        writer.WriteLine($"public static {configuration.Symbol.Name} Instance => _instance.Value;");
        writer.WriteLine();
        writer.WriteLine("/// <summary>将此配置中的全部源生成注册应用到指定服务集合</summary>");
        writer.WriteLine(GeneratorHelper.GeneratedCodeAttributeName);
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
            .Where(static constructor => constructor.HasAttribute(ConstructorAttributeName))
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
            .Append(implementationType.GetGlobalFullName())
            .Append('(')
            .Append(arguments)
            .Append(')');
        if (properties.Count > 0)
        {
            builder.Append(" { ");
            foreach (var property in properties)
            {
                builder
                    .Append(property.Name)
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
                    .Where(static property => property.HasAttribute(PropertyAttributeName))
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

    private static string SanitizeHintName(string name) =>
        new(name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private sealed class ConfigurationInfo(INamedTypeSymbol symbol, ClassDeclarationSyntax syntax)
    {
        public INamedTypeSymbol Symbol { get; } = symbol;
        public ClassDeclarationSyntax Syntax { get; } = syntax;
    }

    private sealed class Registration(
        INamedTypeSymbol? containingType,
        ITypeSymbol serviceType,
        ITypeSymbol implementationType,
        string methodName,
        bool isServiceMapping,
        Location location
    )
    {
        public INamedTypeSymbol? ContainingType { get; } = containingType;
        public ITypeSymbol ServiceType { get; } = serviceType;
        public ITypeSymbol ImplementationType { get; } = implementationType;
        public string MethodName { get; } = methodName;
        public bool IsServiceMapping { get; } = isServiceMapping;
        public Location Location { get; } = location;
    }
}
