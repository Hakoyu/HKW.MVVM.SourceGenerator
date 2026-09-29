using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal class RelayCommandInfo
{
    public RelayCommandInfo(
        string methodName,
        ITypeSymbol? methodReturnType,
        ITypeSymbol? argumentType,
        bool isTask,
        AttributeParamDictionary attributeParams
    )
    {
        MethodName = methodName;
        MethodReturnType = methodReturnType;
        ArgumentType = argumentType;
        IsTask = isTask;
        Attributes = attributeParams;
    }

    public string MethodName { get; set; }
    public ITypeSymbol? MethodReturnType { get; set; }
    public ITypeSymbol? ArgumentType { get; set; }
    public bool IsTask { get; set; }

    /// <summary>
    /// (ParamName, TypeAndValue)
    /// </summary>
    public AttributeParamDictionary Attributes { get; set; }
}
