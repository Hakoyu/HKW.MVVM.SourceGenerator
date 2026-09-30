using System.Collections.Generic;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HKW.MVVM.SourceGenerator;

internal sealed class ClassInfo
{
    public const string SourceName = "_source";
    public const string SourceParamName = "source";

    public ClassInfo(
        SourceProductionContext productionContext,
        Compilation compilation,
        SyntaxTreeInfo syntaxTreeInfo,
        ClassDeclarationSyntax declarationSyntax,
        INamedTypeSymbol classSymbol
    )
    {
        ProductionContext = productionContext;
        Compilation = compilation;
        DeclarationSyntax = declarationSyntax;
        ClassSymbol = classSymbol;
        Name = classSymbol.Name;
        Namespace = classSymbol.ContainingNamespace.ToString();
        Usings = (
            (CompilationUnitSyntax)syntaxTreeInfo.SyntaxTree.GetRoot(CancellationToken.None)
        ).Usings;
        ChangeArgsCache = new(compilation);

        // 分析所有成员
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IMethodSymbol methodSymbol)
            {
                Methods.Add(methodSymbol);
            }
            else if (member is IPropertySymbol propertySymbol)
            {
                Propertys.Add(propertySymbol);
            }
        }

        HelperPropertyName = Name + "ObservableHelper";
        HelperObjectName = Name + "ObservableObjectHelper";
        var observableHelperField = new FieldGenerateInfo(
            HelperObjectName,
            "_" + HelperPropertyName.FirstLetterToLower()
        )
        {
            Default = "default!",
        };
        var observableHelperProperty = new PropertyGenerateInfo(
            HelperObjectName,
            HelperPropertyName,
            new($"=> {observableHelperField.Name} ?? ({observableHelperField.Name} = new(this));")
        )
        {
            Accessibility = classSymbol.IsSealed ? Accessibility.Private : Accessibility.Protected,
        };
        Members.Add(observableHelperField);
        Members.Add(observableHelperProperty);
    }

    public Compilation Compilation { get; }
    public SourceProductionContext ProductionContext { get; }

    public string Namespace { get; }
    public string Name { get; }
    public string TypeName => $"{Name}{DeclarationSyntax.TypeParameterList}";
    public string FullName => $"{Namespace}.{Name}";
    public string FullTypeName => $"{Namespace}.{Name}{DeclarationSyntax.TypeParameterList}";
    public List<IMethodSymbol> Methods { get; } = [];
    public List<IPropertySymbol> Propertys { get; } = [];
    public SyntaxList<UsingDirectiveSyntax> Usings { get; }
    public ClassDeclarationSyntax DeclarationSyntax { get; }
    public INamedTypeSymbol ClassSymbol { get; }

    public string HelperPropertyName { get; }

    public string HelperObjectName { get; }

    public List<IMemberGenerateInfo> Members { get; } = [];
    public List<IMemberGenerateInfo> HelperMembers { get; } = [];

    /// <summary>
    /// 所有初始化成员
    /// </summary>
    public List<string> InitializeMembers { get; } = [];

    /// <summary>
    /// (Property, Actions)
    /// </summary>
    public Dictionary<string, List<string>> PropertyChangedMemberByName { get; } = [];

    /// <summary>
    /// (Property, Actions)
    /// </summary>
    public Dictionary<string, List<string>> PropertyChangingMemberByName { get; } = [];

    public ChangeArgsCache ChangeArgsCache { get; }
}

internal sealed class ChangeArgsCache
{
    public ChangeArgsCache(Compilation compilation)
    {
        var assemblyName = compilation.AssemblyName!.Replace(".", "_");
        ChangingName = $"{assemblyName}_PropertyChangingArgsCache";
        ChangedName = $"{assemblyName}_PropertyChangedArgsCache";
    }

    public string ChangingName { get; }
    public string ChangedName { get; }
    public Dictionary<string, PropertyGenerateInfo> ChangingArgs { get; } = [];
    public Dictionary<string, PropertyGenerateInfo> ChangedArgs { get; } = [];

    public const string Namespace = "HKW.MVVM.SourceGenerator";

    public string GetChangingArgs(string propertyName)
    {
        if (ChangingArgs.TryGetValue(propertyName, out var property) is false)
        {
            property = ChangingArgs[propertyName] = new(
                MVVMGenerator.PropertyChangingEventArgs,
                propertyName,
                new()
            )
            {
                IsStatic = true,
                Default = $"new(\"{propertyName}\")",
                Accessibility = Accessibility.Public,
            };
        }
        return $"{GeneratorHelper.GlobalPrefix}{Namespace}.{ChangingName}.{property.Name}";
    }

    public string GetChangedArgs(string propertyName)
    {
        if (ChangedArgs.TryGetValue(propertyName, out var property) is false)
        {
            property = ChangedArgs[propertyName] = new(
                MVVMGenerator.PropertyChangedEventArgs,
                propertyName,
                new()
            )
            {
                IsStatic = true,
                Default = $"new(\"{propertyName}\")",
                Accessibility = Accessibility.Public,
            };
        }
        return $"{GeneratorHelper.GlobalPrefix}{Namespace}.{ChangedName}.{property.Name}";
    }
}
