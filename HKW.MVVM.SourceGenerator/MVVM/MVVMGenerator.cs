// Source from https://github.com/SparkyTD/ReactiveCommand.SourceGenerator

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HKW.MVVM.SourceGenerator;

[Generator]
internal partial class MVVMGenerator : IIncrementalGenerator
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

    public static string PropertyChangingEventArgs { get; } =
        typeof(PropertyChangingEventArgs).GetGlobalFullName();
    public static string PropertyChangedEventArgs { get; } =
        typeof(PropertyChangedEventArgs).GetGlobalFullName();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var observableClasses = context
            .SyntaxProvider.CreateSyntaxProvider<SyntaxTree>(
                static (node, _) => node is ClassDeclarationSyntax,
                static (syntaxContext, _) =>
                {
                    var declaredClass = (ClassDeclarationSyntax)syntaxContext.Node;
                    var classSymbol = syntaxContext.SemanticModel.GetDeclaredSymbol(declaredClass);
                    // 如果没有继承ObservableObject,则为null
                    return classSymbol?.InheritedFrom(MVVMGenerator.ObservableObject) is true
                        ? declaredClass.SyntaxTree
                        : null!;
                }
            )
            .Where(static syntaxTree => syntaxTree is not null)
            .Select(static (syntaxTree, _) => syntaxTree)
            .Collect();

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(observableClasses),
            static (spc, input) =>
            {
                GeneratorHelper.Initialize();

                foreach (var syntaxTree in input.Right.Distinct())
                {
                    ParseSyntaxTree(spc, input.Left, syntaxTree);
                }
            }
        );
    }

    private static void ParseSyntaxTree(
        SourceProductionContext productionContext,
        Compilation compilation,
        SyntaxTree syntaxTree
    )
    {
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var syntaxTreeInfo = new SyntaxTreeInfo(syntaxTree, semanticModel);
        var declaredClasses = syntaxTree
            .GetRoot(CancellationToken.None)
            .DescendantNodesAndSelf()
            .OfType<ClassDeclarationSyntax>();
        foreach (var declaredClass in declaredClasses)
        {
            if (
                ClassValidator(productionContext, compilation, syntaxTreeInfo, declaredClass)
                is not ClassInfo classInfo
            )
                continue;

            NotifyPropertyChangeFromGenerator.Generate(classInfo);
            ObservablePropertyGenerator.Generate(classInfo);
            RelayCommandGenerator.Generate(classInfo);
            ObservableAsPropertyGenerator.Generate(classInfo);

            ClassSourceWriter.Execute(classInfo);
        }
    }

    private static ClassInfo? ClassValidator(
        SourceProductionContext productionContext,
        Compilation compilation,
        SyntaxTreeInfo syntaxTreeInfo,
        ClassDeclarationSyntax declaredClass
    )
    {
        var classSymbol = (INamedTypeSymbol)
            ModelExtensions.GetDeclaredSymbol(
                syntaxTreeInfo.SemanticModel,
                declaredClass,
                CancellationToken.None
            )!;
        if (classSymbol.InheritedFrom(MVVMGenerator.ObservableObject) is false)
            return null; // 如果没有继承ObservableObject,则跳过

        // 如果不是分布类型,则触发异常
        if (declaredClass.Modifiers.Any(SyntaxKind.PartialKeyword) is false)
        {
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.NotPartialClass,
                classSymbol.Locations[0]
            );
            productionContext.ReportDiagnostic(diagnostic);
            return null;
        }

        var classInfo = new ClassInfo(
            productionContext,
            compilation,
            syntaxTreeInfo,
            declaredClass,
            classSymbol
        );
        return classInfo;
    }
}
