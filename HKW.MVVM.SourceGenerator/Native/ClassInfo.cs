using System.Collections.Generic;
using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HKW.MVVM.SourceGenerator;

internal sealed class ClassInfo
{
    public ClassInfo(
        SyntaxTreeInfo syntaxTreeInfo,
        ClassDeclarationSyntax declarationSyntax,
        INamedTypeSymbol classSymbol
    )
    {
        DeclarationSyntax = declarationSyntax;
        ClassSymbol = classSymbol;
        Name = classSymbol.Name;
        Namespace = classSymbol.ContainingNamespace.ToString();
        Usings = ((CompilationUnitSyntax)syntaxTreeInfo.SyntaxTree.GetRoot()).Usings;

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

    public string Namespace { get; }
    public string Name { get; }
    public string TypeName => $"{Name}{DeclarationSyntax.TypeParameterList}";
    public string FullName => $"{Namespace}.{Name}";
    public string FullTypeName => $"{Namespace}.{Name}{DeclarationSyntax.TypeParameterList}";
    public List<MethodSS> MethodSSs { get; } = [];
    public List<PropertySS> PropertySSs { get; } = [];
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
}
