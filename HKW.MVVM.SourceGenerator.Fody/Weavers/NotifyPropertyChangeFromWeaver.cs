using Mono.Cecil;
using Mono.Cecil.Cil;

namespace HKW.MVVM.Fody;

internal class NotifyPropertyChangeFromWeaver
{
    public static void Weave(ClassInfo classInfo)
    {
        ModuleWeaver.Logger.LogInfo(nameof(NotifyPropertyChangeFromWeaver));
        var w = new NotifyPropertyChangeFromWeaver(classInfo);
        w.Weave();
    }

    private readonly ClassInfo _classInfo;

    public NotifyPropertyChangeFromWeaver(ClassInfo classInfo)
    {
        _classInfo = classInfo;
    }

    public void Weave()
    {
        foreach (var property in _classInfo.Type.Properties)
        {
            WeaveProperty(property);
        }
    }

    public void WeaveProperty(PropertyDefinition property)
    {
        if (property.IsDefined(WeaverHelper.NotifyPropertyChangeFromAttribute) is false)
            return;

        var attributeParameters = property
            .CustomAttributes.First(x =>
                x.AttributeType.FullName == WeaverHelper.NotifyPropertyChangeFromAttribute.FullName
            )
            .GetParams();
        if (
            attributeParameters.TryGetValue("CacheMode", out var cacheModeParameter) is false
            || cacheModeParameter.Value is 0
        )
            return;

        // 如果启用了缓存,则会生成一个新字段来缓存值
        var fieldName = $"_{property.Name.FirstLetterToLower()}Cache";
        var field =
            _classInfo.HelperType.Fields.FirstOrDefault(x => x.Name == fieldName)
            ?? throw new WeaverException($"Field {fieldName} not exist");

        property.GetMethod.Body = new MethodBody(property.GetMethod);
        property.GetMethod.Body.Emit(il =>
        {
            // this
            il.Emit(OpCodes.Ldarg_0);
            // Helper
            il.Emit(OpCodes.Call, _classInfo.HelperPropertyGetMethod);
            // this.Helper._cache
            il.Emit(OpCodes.Ldfld, field.BindDefinition(_classInfo.HelperType));
            // Return
            il.Emit(OpCodes.Ret);
        });
    }
}
