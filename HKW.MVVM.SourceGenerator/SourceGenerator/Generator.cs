// Source from https://github.com/SparkyTD/ReactiveCommand.SourceGenerator

using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Reflection;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HKW.MVVM.SourceGenerator;

[Generator]
internal partial class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(
            context.CompilationProvider,
            static (spc, compilation) =>
            {
                GeneratorHelper.Initialize(spc, compilation);
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    ParseSyntaxTree(syntaxTree);
                }
            }
        );
    }

    private static void ParseSyntaxTree(SyntaxTree syntaxTree)
    {
        var semanticModel = GeneratorHelper.Compilation.GetSemanticModel(syntaxTree);
        var syntaxTreeInfo = new SyntaxTreeInfo(syntaxTree, semanticModel);
        var declaredClasses = syntaxTree
            .GetRoot()
            .DescendantNodesAndSelf()
            .OfType<ClassDeclarationSyntax>();
        foreach (var declaredClass in declaredClasses)
        {
            if (ClassValidator(syntaxTreeInfo, declaredClass) is not ClassInfo classInfo)
                continue;

            NotifyPropertyChangeFromGenerator.Generate(classInfo);
            ObservablePropertyGenerator.Generate(classInfo);
            RelayCommandGenerator.Generate(classInfo);

            if (ClassSourceWriter.FirstClassFullName == string.Empty)
                ClassSourceWriter.FirstClassFullName = classInfo.FullTypeName;

            ClassSourceWriter.Execute(classInfo);
        }
    }

    private static ClassInfo? ClassValidator(
        SyntaxTreeInfo syntaxTreeInfo,
        ClassDeclarationSyntax declaredClass
    )
    {
        var classSymbol = (INamedTypeSymbol)
            ModelExtensions.GetDeclaredSymbol(syntaxTreeInfo.SemanticModel, declaredClass)!;
        if (classSymbol.InheritedFromX(TypeFullNames.ObservableObject) is false)
            return null; // 如果没有继承ObservableObject,则跳过

        // 如果不是分布类型,则触发异常
        if (declaredClass.Modifiers.Any(SyntaxKind.PartialKeyword) is false)
        {
            var diagnostic = Diagnostic.Create(
                Descriptors.NotPartialClass,
                classSymbol.Locations[0]
            );
            GeneratorHelper.ProductionContext.ReportDiagnostic(diagnostic);
            return null;
        }

        var classInfo = new ClassInfo(syntaxTreeInfo, declaredClass, classSymbol);

        // 分析所有成员
        foreach (var member in declaredClass.Members)
        {
            if (member is MethodDeclarationSyntax methodSyntax)
            {
                methodSyntax.GetLocation();
                var methodSymbol = (IMethodSymbol)
                    ModelExtensions.GetDeclaredSymbol(syntaxTreeInfo.SemanticModel, methodSyntax)!;
                classInfo.MethodSSs.Add(new(methodSyntax, methodSymbol));
            }
            else if (member is PropertyDeclarationSyntax propertySyntax)
            {
                var propertySymbol = (IPropertySymbol)
                    ModelExtensions.GetDeclaredSymbol(
                        syntaxTreeInfo.SemanticModel,
                        propertySyntax
                    )!;
                classInfo.PropertySSs.Add(new(propertySyntax, propertySymbol));
            }
        }
        return classInfo;
    }
}
