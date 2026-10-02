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
        var registrationScans = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax,
                static (syntaxContext, _) => GetRegistrations(syntaxContext)
            )
            .Where(static scan => scan.IsEmpty is false)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(configurations).Combine(registrationScans),
            static (productionContext, input) =>
                Generate(productionContext, input.Left.Left, input.Left.Right, input.Right)
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

    private static RegistrationScan GetRegistrations(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (
            context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart)
                is not IMethodSymbol enclosingMethod
            || IsConfigureMethod(enclosingMethod) is false
        )
            return RegistrationScan.Empty;

        if (
            method.ContainingType.ToDisplayString() == RegistrationsTypeName
            && method.Parameters.Length == 0
            && method.TypeArguments.Length is >= 1 and <= 2
        )
        {
            return new RegistrationScan(
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
                ],
                []
            );
        }

        var attributeInfo = method.GetFirstAttribute(CustomRegistrarAttributeName).GetInfo();
        if (attributeInfo is null)
            return RegistrationScan.Empty;

        var attributeLocation =
            attributeInfo.Data.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? invocation.GetLocation();
        var genericNameValues = attributeInfo
            .GetParams<string>(nameof(DICustomServiceRegistrarAttribute.GenericNames))
            .ToArray();
        var registrationValues = attributeInfo.GetParams<DIServiceRegistration>(
            nameof(DICustomServiceRegistrarAttribute.Registrations)
        );
        if (registrationValues is not null && registrationValues.Length != genericNameValues.Length)
        {
            return new RegistrationScan(
                [],
                [
                    Diagnostic.Create(
                        DIDescriptors.CustomServiceRegistrarRegistrationCountMismatch,
                        attributeLocation,
                        method.Name,
                        genericNameValues.Length,
                        registrationValues.Length
                    ),
                ]
            );
        }

        var registrations = ImmutableArray.CreateBuilder<Registration>();
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        for (var index = 0; index < genericNameValues.Length; index++)
        {
            var genericName = genericNameValues[index];
            var genericParam = method.TypeParameters.FirstOrDefault(parameter =>
                parameter.Name == genericName
            );
            if (genericParam is null)
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        DIDescriptors.InvalidCustomServiceRegistrarGenericName,
                        attributeLocation,
                        genericName,
                        method.Name
                    )
                );
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
        return new RegistrationScan(registrations.ToImmutable(), diagnostics.ToImmutable());
    }

    private static bool IsConfigureMethod(IMethodSymbol method) =>
        method.Name == "Configure"
        && method.IsOverride
        && method.OverriddenMethod?.ContainingType.ToDisplayString() == RegistrationsTypeName;

    private static void Generate(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ConfigurationInfo> configurations,
        ImmutableArray<RegistrationScan> registrationScans
    )
    {
        foreach (var diagnostic in registrationScans.SelectMany(scan => scan.Diagnostics))
            context.ReportDiagnostic(diagnostic);

        var registrations = registrationScans
            .SelectMany(static scan => scan.Registrations)
            .ToImmutableArray();
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

            var configurationRegistrations = registrations
                .Where(registration =>
                    SymbolEqualityComparer.Default.Equals(
                        registration.ContainingType,
                        configuration.Symbol
                    )
                )
                .OrderBy(registration => registration.Location.SourceTree?.FilePath)
                .ThenBy(registration => registration.Location.SourceSpan.Start)
                .ToImmutableArray();
            var uniqueRegistrations = FilterDuplicateRegistrations(
                context,
                configurationRegistrations
            );
            AnalyzeDependencyGraph(context, compilation, uniqueRegistrations);
            AddConfigurationSource(context, compilation, configuration, uniqueRegistrations);
        }
    }

    private static ImmutableArray<Registration> FilterDuplicateRegistrations(
        SourceProductionContext context,
        ImmutableArray<Registration> registrations
    )
    {
        var uniqueRegistrations = ImmutableArray.CreateBuilder<Registration>();
        foreach (var registration in registrations)
        {
            if (uniqueRegistrations.Any(existing => IsSameRegistration(existing, registration)))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DIDescriptors.DuplicateRegistration,
                        registration.Location,
                        registration.ServiceType.GetName(),
                        registration.ImplementationType.GetName(),
                        registration.MethodName
                    )
                );
                continue;
            }
            uniqueRegistrations.Add(registration);
        }
        return uniqueRegistrations.ToImmutable();
    }

    private static void AnalyzeDependencyGraph(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<Registration> registrations
    )
    {
        var nodes = new List<RegistrationNode>();
        foreach (var registration in registrations)
        {
            if (registration.ImplementationType is not INamedTypeSymbol implementationType)
                continue;
            var constructor = SelectConstructor(implementationType);
            if (constructor is null)
                continue;
            var dependencies = constructor
                .Parameters.Select(parameter => parameter.Type)
                .Concat(
                    GetInjectedProperties(compilation, implementationType)
                        .Select(property => property.Type)
                )
                .Select(GetDependencyInfo)
                .ToArray();
            nodes.Add(new RegistrationNode(registration, dependencies));
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
            AnalyzeDependencyNode(
                context,
                nodes,
                node,
                new List<RegistrationNode>(),
                null,
                reported
            );
    }

    private static void AnalyzeDependencyNode(
        SourceProductionContext context,
        List<RegistrationNode> nodes,
        RegistrationNode node,
        List<RegistrationNode> path,
        RegistrationNode? singleton,
        HashSet<string> reported
    )
    {
        var cycleIndex = path.IndexOf(node);
        if (cycleIndex >= 0)
        {
            var cycleNodes = path.Skip(cycleIndex).ToArray();
            var chain = string.Join(
                " -> ",
                cycleNodes
                    .Select(item => item.Registration.ServiceType.GetName())
                    .Concat(new[] { node.Registration.ServiceType.GetName() })
            );
            var cycleKey = string.Join(
                "|",
                cycleNodes
                    .Select(item => item.Registration.ServiceType.GetFullName())
                    .OrderBy(name => name, StringComparer.Ordinal)
            );
            if (reported.Add($"cycle:{cycleKey}"))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DIDescriptors.CircularDependency,
                        node.Registration.Location,
                        chain
                    )
                );
            }
            return;
        }

        if (singleton is not null && GetLifetime(node.Registration) == GraphLifetime.Scoped)
        {
            var chain = string.Join(
                " -> ",
                path.Select(item => item.Registration.ServiceType.GetName())
                    .Concat(new[] { node.Registration.ServiceType.GetName() })
            );
            var key = $"scope:{singleton.Registration.ServiceType}:{node.Registration.ServiceType}";
            if (reported.Add(key))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DIDescriptors.CaptiveScopedDependency,
                        singleton.Registration.Location,
                        singleton.Registration.ServiceType.GetName(),
                        node.Registration.ServiceType.GetName(),
                        chain
                    )
                );
            }
            return;
        }

        singleton ??= GetLifetime(node.Registration) == GraphLifetime.Singleton ? node : null;
        path.Add(node);
        foreach (var dependency in node.Dependencies)
        {
            if (IsBuiltInDependency(dependency.Type))
                continue;
            var candidates = nodes
                .Where(candidate =>
                    SymbolEqualityComparer.Default.Equals(
                        candidate.Registration.ServiceType,
                        dependency.Type
                    )
                )
                .ToArray();
            if (candidates.Length == 0)
            {
                if (!dependency.IsEnumerable)
                {
                    var key = $"missing:{node.Registration.ServiceType}:{dependency.Type}";
                    if (reported.Add(key))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                DIDescriptors.PossiblyMissingDependency,
                                node.Registration.Location,
                                node.Registration.ServiceType.GetName(),
                                dependency.Type.GetName()
                            )
                        );
                    }
                }
                continue;
            }

            if (dependency.IsEnumerable)
            {
                foreach (var candidate in candidates)
                    AnalyzeDependencyNode(context, nodes, candidate, path, singleton, reported);
            }
            else
            {
                AnalyzeDependencyNode(
                    context,
                    nodes,
                    candidates[candidates.Length - 1],
                    path,
                    singleton,
                    reported
                );
            }
        }
        path.RemoveAt(path.Count - 1);
    }

    private static GraphLifetime GetLifetime(Registration registration) =>
        registration.MethodName switch
        {
            "RegisterScoped" => GraphLifetime.Scoped,
            "RegisterSingleton" or "RegisterLazySingleton" => GraphLifetime.Singleton,
            _ => GraphLifetime.Transient,
        };

    private static bool IsBuiltInDependency(ITypeSymbol type)
    {
        var name = type.GetFullName();
        return name == "System.IServiceProvider"
            || name == "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory"
            || name == "Microsoft.Extensions.DependencyInjection.IServiceProviderIsService";
    }

    private static bool IsSameRegistration(Registration left, Registration right) =>
        left.MethodName == right.MethodName
        && left.IsServiceMapping == right.IsServiceMapping
        && SymbolEqualityComparer.Default.Equals(left.ServiceType, right.ServiceType)
        && SymbolEqualityComparer.Default.Equals(left.ImplementationType, right.ImplementationType);

    private static bool IsValidConfiguration(ConfigurationInfo configuration) =>
        configuration.Symbol.IsStatic is false
        && configuration.Symbol.IsAbstract is false
        && configuration.Symbol.Arity == 0
        && configuration.Symbol.ContainingType is null
        && configuration.Symbol.BaseType?.GetFullName() == RegistrationsTypeName
        && configuration.Symbol.InstanceConstructors.Any(constructor =>
            constructor.Parameters.Length == 0
            && constructor.DeclaredAccessibility >= Accessibility.Internal
        )
        && configuration.Syntax.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static void AppendRegistration(
        SourceProductionContext context,
        Compilation compilation,
        IndentedTextWriter writer,
        Registration registration,
        bool reportDiagnostics = true
    )
    {
        if (
            TryCreateFactory(context, compilation, registration, reportDiagnostics, out var factory)
            is false
        )
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
        var registrationArray = registrations.ToArray();
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
        foreach (var registration in registrationArray)
            AppendRegistration(context, compilation, writer, registration);
        writer.WriteLine("return services;");
        writer.Indent--;
        writer.WriteLine("}");
        writer.WriteLine();
        AppendGeneratedServiceProvider(compilation, writer, registrationArray);
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

    private static void AppendGeneratedServiceProvider(
        Compilation compilation,
        IndentedTextWriter writer,
        IReadOnlyList<Registration> registrations
    )
    {
        writer.WriteLine("/// <inheritdoc/>");
        writer.WriteLine(GeneratorHelper.GeneratedCodeAttributeName);
        writer.WriteLine(
            "public override global::HKW.MVVM.SourceGenerator.DIServiceProvider BuildServiceProvider()"
        );
        writer.WriteLine(
            "    => BuildServiceProvider(new global::HKW.MVVM.SourceGenerator.ServiceProviderOptions());"
        );
        writer.WriteLine();
        writer.WriteLine("/// <inheritdoc/>");
        writer.WriteLine(GeneratorHelper.GeneratedCodeAttributeName);
        writer.WriteLine(
            "public override global::HKW.MVVM.SourceGenerator.DIServiceProvider BuildServiceProvider("
        );
        writer.Indent++;
        writer.WriteLine("global::HKW.MVVM.SourceGenerator.ServiceProviderOptions options)");
        writer.Indent--;
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("if (options is null)");
        writer.Indent++;
        writer.WriteLine("throw new global::System.ArgumentNullException(nameof(options));");
        writer.Indent--;
        writer.WriteLine(
            "var services = Build(new global::Microsoft.Extensions.DependencyInjection.ServiceCollection());"
        );
        writer.WriteLine(
            "var metadata = new global::HKW.MVVM.SourceGenerator.DIServiceRegistrationMetadata[]"
        );
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var registration in registrations)
            AppendRegistrationMetadata(compilation, writer, registration);
        writer.Indent--;
        writer.WriteLine("};");
        writer.WriteLine(
            "return new global::HKW.MVVM.SourceGenerator.DIServiceProvider(services, options, metadata);"
        );
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static void AppendRegistrationMetadata(
        Compilation compilation,
        IndentedTextWriter writer,
        Registration registration
    )
    {
        if (registration.ImplementationType is not INamedTypeSymbol implementationType)
            return;
        var constructor = SelectConstructor(implementationType);
        if (constructor is null)
            return;
        var dependencies = constructor
            .Parameters.Select(parameter => parameter.Type)
            .Concat(
                GetInjectedProperties(compilation, implementationType)
                    .Select(property => property.Type)
            )
            .Select(GetDependencyInfo)
            .ToArray();
        var lifetime = registration.MethodName switch
        {
            "RegisterScoped" => "Scoped",
            "RegisterSingleton" or "RegisterLazySingleton" => "Singleton",
            _ => "Transient",
        };
        writer.WriteLine("new global::HKW.MVVM.SourceGenerator.DIServiceRegistrationMetadata(");
        writer.Indent++;
        writer.WriteLine($"typeof({registration.ServiceType.GetGlobalFullName()}),");
        writer.WriteLine(
            $"global::Microsoft.Extensions.DependencyInjection.ServiceLifetime.{lifetime},"
        );
        writer.WriteLine("new global::HKW.MVVM.SourceGenerator.DIServiceDependencyMetadata[]");
        writer.WriteLine("{");
        writer.Indent++;
        foreach (var dependency in dependencies)
        {
            writer.WriteLine(
                $"new global::HKW.MVVM.SourceGenerator.DIServiceDependencyMetadata(typeof({dependency.Type.GetGlobalFullName()}), {dependency.IsEnumerable.ToString().ToLowerInvariant()}),"
            );
        }
        writer.Indent--;
        writer.WriteLine("},");
        writer.WriteLine("false");
        writer.Indent--;
        writer.WriteLine("),");
    }

    private static IMethodSymbol? SelectConstructor(INamedTypeSymbol implementationType)
    {
        var constructors = implementationType.InstanceConstructors;
        var markedConstructors = constructors
            .Where(static constructor => constructor.HasAttribute(ConstructorAttributeName))
            .ToImmutableArray();
        if (constructors.Length == 1 && markedConstructors.Length <= 1)
            return constructors[0];
        return markedConstructors.Length == 1 ? markedConstructors[0] : null;
    }

    private static DependencyInfo GetDependencyInfo(ITypeSymbol type)
    {
        if (
            type is INamedTypeSymbol namedType
            && namedType.IsGenericType
            && namedType.TypeArguments.Length == 1
        )
        {
            var definition = namedType.OriginalDefinition.ToDisplayString();
            if (definition == "System.Lazy<T>")
                return new DependencyInfo(namedType.TypeArguments[0], false);
            if (definition == "System.Collections.Generic.IEnumerable<T>")
                return new DependencyInfo(namedType.TypeArguments[0], true);
        }
        return new DependencyInfo(type, false);
    }

    private static bool TryCreateFactory(
        SourceProductionContext context,
        Compilation compilation,
        Registration registration,
        bool reportDiagnostics,
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
            if (reportDiagnostics)
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
            if (reportDiagnostics)
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
            if (reportDiagnostics)
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
            if (reportDiagnostics)
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
                if (reportDiagnostics)
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

    private enum GraphLifetime
    {
        Transient,
        Scoped,
        Singleton,
    }

    private sealed class DependencyInfo(ITypeSymbol type, bool isEnumerable)
    {
        public ITypeSymbol Type { get; } = type;
        public bool IsEnumerable { get; } = isEnumerable;
    }

    private sealed class RegistrationNode(Registration registration, DependencyInfo[] dependencies)
    {
        public Registration Registration { get; } = registration;
        public DependencyInfo[] Dependencies { get; } = dependencies;
    }

    private sealed class RegistrationScan(
        ImmutableArray<Registration> registrations,
        ImmutableArray<Diagnostic> diagnostics
    )
    {
        public static RegistrationScan Empty { get; } = new([], []);

        public ImmutableArray<Registration> Registrations { get; } = registrations;
        public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
        public bool IsEmpty => Registrations.IsDefaultOrEmpty && Diagnostics.IsDefaultOrEmpty;
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
