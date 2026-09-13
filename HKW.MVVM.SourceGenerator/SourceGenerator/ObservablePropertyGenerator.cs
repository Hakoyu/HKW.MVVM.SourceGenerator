using System.ComponentModel;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal class ObservablePropertyGenerator
{
    public static void Generate(ClassInfo classInfo)
    {
        var analyzer = new ObservablePropertyGenerator(classInfo);
        analyzer.Execute();
    }

    private readonly ClassInfo _classInfo;

    public ObservablePropertyGenerator(ClassInfo classInfo)
    {
        _classInfo = classInfo;
    }

    private void Execute()
    {
        for (var i = 0; i < _classInfo.PropertySSs.Count; i++)
        {
            var property = _classInfo.PropertySSs[i];
            AnalyzeProperty(property);
        }
    }

    private void AnalyzeProperty(PropertySS ss)
    {
        ss.OutData(out var propertySyntax, out var propertySymbol);
        if (propertySymbol.GetFirstAttribute(TypeFullNames.ObservablePropertyAttribute) is null)
            return;
        // 如果没有Set方法则异常
        if (propertySymbol.SetMethod is null)
        {
            var diagnostic = Diagnostic.Create(
                Descriptors.PropertyNotHaveSetMethod,
                propertySyntax.GetLocation(),
                nameof(TypeFullNames.ObservablePropertyAttribute)
            );
            GeneratorHelper.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
        var typeName = propertySymbol.Type.GetFullName();

        GeneratePartialMethod(propertySymbol);
        var contents = GenerateSetMethodContexts(propertySymbol);

        var raiseMethod = new MethodGenerateInfo(
            GeneratorHelper.TypeVoid,
            $"SetProperty{propertySymbol.Name}",
            contents
        )
        {
            Accessibility = Accessibility.Public,
            Params = new()
            {
                new(typeName, "backingField") { GenerateType = ParameterGenerateType.Ref },
                new(typeName, "newValue"),
            },
        };
        _classInfo.HelperMembers.Add(raiseMethod);
    }

    public List<string> GenerateSetMethodContexts(IPropertySymbol property)
    {
        var contents = new List<string>();
        contents.Add(
            $"if (global::System.Collections.Generic.EqualityComparer<{property.Type.GetFullName()}>.Default.Equals(backingField, newValue))"
        );
        contents.Add("    return;");
        contents.Add("var oldValue = backingField;");
        contents.Add($"_source.OnPropertyChanging(\"{property.Name}\");");
        contents.Add($"var cancel = false;");
        contents.Add($"On{property.Name}Changing(oldValue,newValue,ref cancel);");
        contents.Add($"if(cancel) return;");
        if (
            _classInfo.PropertyChangingMemberByName.TryGetValue(
                property.Name,
                out var changingActions
            )
        )
        {
            contents.Add("");
            foreach (var action in changingActions)
                contents.Add(action);
        }

        contents.Add("");
        contents.Add("backingField = newValue;");
        contents.Add("");

        contents.Add($"_source.OnPropertyChanged(\"{property.Name}\");");
        contents.Add($"On{property.Name}Changed(oldValue,newValue);");

        if (
            _classInfo.PropertyChangedMemberByName.TryGetValue(
                property.Name,
                out var changedActions
            )
        )
        {
            contents.Add("");
            foreach (var action in changedActions)
                contents.Add(action);
        }

        return contents;
    }

    public void GeneratePartialMethod(IPropertySymbol property)
    {
        var typeName = property.Type.GetFullName();
        _classInfo.HelperMembers.Add(
            new MethodGenerateInfo(GeneratorHelper.TypeVoid, $"On{property.Name}Changing", "")
            {
                Params =
                [
                    new(typeName, "oldValue"),
                    new(typeName, "newValue"),
                    new("bool", "cancel") { GenerateType = ParameterGenerateType.Ref },
                ],
                GenerateType = MethodGenerateType.Partial,
            }
        );
        _classInfo.HelperMembers.Add(
            new MethodGenerateInfo(GeneratorHelper.TypeVoid, $"On{property.Name}Changed", "")
            {
                Params = [new(typeName, "oldValue"), new(typeName, "newValue")],
                GenerateType = MethodGenerateType.Partial,
            }
        );
    }
}
