using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace HKW.MVVM.SourceGenerator.Fody;

internal class ReactivePropertyWeaver
{
    public static void Weave(ClassInfo classInfo)
    {
        ModuleWeaver.Logger.LogInfo(nameof(ReactivePropertyWeaver));
        var w = new ReactivePropertyWeaver(classInfo);
        w.Weave();
    }

    private readonly ClassInfo _classInfo;

    public ReactivePropertyWeaver(ClassInfo classInfo)
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
        if (property.IsDefined(WeaverHelper.ObservablePropertyAttribute) is false)
            return;

        // 如果没有SetMethod
        if (property.SetMethod is null)
        {
            ModuleWeaver.Logger.LogError(
                $"Property {property.DeclaringType.FullName}.{property.Name} has no setter, therefore it is not possible for the property to change, and thus should not be marked with [ReactiveProperty]"
            );
            return;
        }

        var setPropertyMethodDefinition =
            _classInfo
                .HelperType.Resolve()
                .Methods.SingleOrDefault(x => x.Name == $"SetProperty{property.Name}")
            ?? throw new WeaverException(
                $"{_classInfo.HelperType.FullName} not exists method SetProperty{property.Name}, please check if you have added the partial keyword to class"
            );

        var setPropertyMethod = _classInfo.IsGeneric
            ? setPropertyMethodDefinition.Bind(
                _classInfo.HelperType.MakeGenericInstanceType([
                    .. _classInfo.HelperType.GenericParameters,
                ])
            )
            : setPropertyMethodDefinition;

        setPropertyMethod = WeaverHelper.ModuleDefinition.ImportReference(setPropertyMethod);

        // 生成一个新字段, 命名为 $PropertyName
        var field = new FieldDefinition(
            "$" + property.Name,
            FieldAttributes.Private,
            property.PropertyType
        );
        WeaverHelper.AddGeneratedCodeAttribute(field);

        _classInfo.Type.Fields.Add(field);

        // 寻找旧字段并删除
        var oldField = (FieldReference)
            property.GetMethod.Body.Instructions.Single(x => x.Operand is FieldReference).Operand;
        var oldFieldDefinition = oldField.Resolve();
        _classInfo.Type.Fields.Remove(oldFieldDefinition);

        // 查看是否存在自动属性初始化器
        var constructors = _classInfo.Type.Methods.Where(x => x.IsConstructor);
        foreach (
            var (constructor, fieldAssignment) in from constructor in constructors
            let fieldAssignment = constructor.Body.Instructions.SingleOrDefault(x =>
                Equals(x.Operand, oldFieldDefinition)
                || Equals(x.Operand?.ToString(), oldField.ToString())
            )
            select (constructor, fieldAssignment)
        )
        {
            if (fieldAssignment is null)
                continue;
            //使用新字段初始化器替换自动生成的初始化器
            if (_classInfo.IsGeneric)
            {
                constructor
                    .Body.GetILProcessor()
                    .Replace(
                        fieldAssignment,
                        Instruction.Create(
                            fieldAssignment.OpCode,
                            field.BindDefinition(_classInfo.Type)
                        )
                    );
            }
            else
            {
                constructor
                    .Body.GetILProcessor()
                    .Replace(fieldAssignment, Instruction.Create(OpCodes.Stfld, field));
            }
        }

        // 创建 getter
        property.GetMethod.Body = new MethodBody(property.GetMethod);
        property.GetMethod.Body.Emit(il =>
        {
            // this
            il.Emit(OpCodes.Ldarg_0);
            // this.$PropertyName
            il.Emit(OpCodes.Ldfld, field.BindDefinition(_classInfo.Type));
            // Return
            il.Emit(OpCodes.Ret);
        });

        // 创建 setter
        property.SetMethod.Body = new MethodBody(property.SetMethod);
        property.SetMethod.Body.Emit(il =>
        {
            // this
            il.Emit(OpCodes.Ldarg_0);
            // this.Helper
            il.Emit(OpCodes.Call, _classInfo.HelperPropertyGetMethod);
            // ref this
            il.Emit(OpCodes.Ldarg_0);
            // ref this.field
            il.Emit(OpCodes.Ldflda, field.BindDefinition(_classInfo.Type));
            // value
            il.Emit(OpCodes.Ldarg_1);
            // this.Helper.SetProperty(ref this.field, value)
            il.Emit(OpCodes.Callvirt, setPropertyMethod);
            // Return
            il.Emit(OpCodes.Ret);
        });
    }
}
