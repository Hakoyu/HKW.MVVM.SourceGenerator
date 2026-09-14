using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace HKW.MVVM.SourceGenerator.Fody;

internal sealed class ClassInfo
{
    public ClassInfo(TypeDefinition type)
    {
        Type = type;
        TypeName = type.Name.GetRealName();

        var helperPropertytName = TypeName + "ObservableHelper";
        var helperObjectName = TypeName + "ObservableObjectHelper";
        IsGeneric = Type.GenericParameters.Count > 0;

        HelperType =
            type.NestedTypes.SingleOrDefault(x => x.Name == helperObjectName)
            ?? throw new WeaverException($"Class {type.Name} has no {helperObjectName}");
        var helperProperty =
            type.Properties.SingleOrDefault(x => x.Name == helperPropertytName)
            ?? throw new WeaverException($"Class {type.Name} has no {helperPropertytName}");

        if (IsGeneric)
        {
            GenericType = Type.MakeGenericInstanceType([.. Type.GenericParameters]);
            HelperPropertyGetMethod = helperProperty.GetMethod.Bind(GenericType);
        }
        else
        {
            HelperPropertyGetMethod = helperProperty.GetMethod;
        }
    }

    public TypeDefinition Type { get; }
    public string TypeName { get; }
    public TypeDefinition HelperType { get; }
    public MethodReference HelperPropertyGetMethod { get; }
    public bool IsGeneric { get; }
    public GenericInstanceType GenericType { get; } = null!;
}
