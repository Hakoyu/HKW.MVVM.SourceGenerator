using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace HKW.MVVM.SourceGenerator.Fody;

/// <summary>
/// Weaver that converts observables as property helper.
/// </summary>
internal class ObservableAsPropertyWeaver
{
    public static void Weave(ClassInfo classInfo)
    {
        if (WeaverHelper.HKWMVVM is null)
            return;
        ModuleWeaver.Logger.LogInfo(nameof(ObservableAsPropertyWeaver));
        var w = new ObservableAsPropertyWeaver(classInfo);
        w.Weave();
    }

    private readonly ClassInfo _classInfo;
    private static MethodReference ObservableAsPropertyHelperGetValue { get; set; } = null!;

    public ObservableAsPropertyWeaver(ClassInfo classType)
    {
        _classInfo = classType;

        if (ObservableAsPropertyHelperGetValue is null)
        {
            ObservableAsPropertyHelperGetValue = WeaverHelper.ModuleDefinition.ImportReference(
                WeaverHelper
                    .ObservableAsPropertyHelper.Resolve()
                    .Properties.Single(x => x.Name == "Value")
                    .GetMethod
            );
        }
    }

    public void Weave()
    {
        foreach (var property in _classInfo.Type.Properties)
        {
            WeaveProperty(property);
        }
    }

    /// <summary>
    /// Executes this property weaver.
    /// </summary>
    public void WeaveProperty(PropertyDefinition property)
    {
        if (property.IsDefined(WeaverHelper.ObservableAsPropertyAttribute) is false)
            return;

        var genericObservableAsPropertyHelper =
            WeaverHelper.ObservableAsPropertyHelper.MakeGenericInstanceType(property.PropertyType);
        var genericObservableAsPropertyHelperGetValue = ObservableAsPropertyHelperGetValue.Bind(
            genericObservableAsPropertyHelper
        );
        WeaverHelper.ModuleDefinition.ImportReference(genericObservableAsPropertyHelperGetValue);

        var fieldName = "_" + property.Name.FirstLetterToLower() + "OAPH";
        var field =
            _classInfo.HelperType.Fields.FirstOrDefault(x => x.Name == fieldName)
            ?? throw new WeaverException($"Field {fieldName} not exist");

        property.GetMethod.Body = new MethodBody(property.GetMethod);
        property.GetMethod.Body.Emit(il =>
        {
            // this
            il.Emit(OpCodes.Ldarg_0);
            // this.Helper
            il.Emit(OpCodes.Call, _classInfo.HelperPropertyGetMethod);
            // this.Helper._OAPH
            il.Emit(OpCodes.Ldfld, field.BindDefinition(_classInfo.HelperType));
            // this.Helper._OAPH.Value
            il.Emit(OpCodes.Callvirt, genericObservableAsPropertyHelperGetValue);
            // Return
            il.Emit(OpCodes.Ret);
        });
    }
}
