using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal class ObservableAsPropertyGenerator
{
    public static void Generate(ClassInfo classInfo)
    {
        var analyzer = new ObservableAsPropertyGenerator(classInfo);
        analyzer.Execute();
    }

    private readonly ClassInfo _classInfo;

    public ObservableAsPropertyGenerator(ClassInfo classInfo)
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
        if (propertySymbol.GetFirstAttribute(TypeFullNames.ObservableAsPropertyAttribute) is null)
            return;
        // 如果有Set方法则异常
        if (propertySymbol.SetMethod is not null)
        {
            var diagnostic = Diagnostic.Create(
                Descriptors.PropertyHasSetMethod,
                propertySyntax.GetLocation(),
                nameof(TypeFullNames.ObservableAsPropertyAttribute)
            );
            GeneratorHelper.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
        if (propertySymbol.TryGetGetMethodContent(out var getMethod) is false)
            return;
        // 如果不是ToProperty方法则取消
        if (getMethod.Contains(".ToProperty(") is false)
            return;
        if (getMethod.EndsWith(".Value;"))
        {
            getMethod = getMethod.Substring(0, getMethod.Length - 7) + ";";
        }
        else if (getMethod.EndsWith(".Value!;"))
        {
            getMethod = getMethod.Substring(0, getMethod.Length - 8) + ";";
        }
        else
        {
            return;
        }
        getMethod = getMethod.Replace("this", "_source");
        var oaphType = $"ObservableAsPropertyHelper<{propertySymbol.Type.GetFullName()}>";
        var oaphInitializaMethodName = propertySymbol.Name + "OAPHInitializa";
        var field = "_" + propertySymbol.Name.FirstLetterToLower() + "OAPH";

        _classInfo.HelperMembers.Add(
            new FieldGenerateInfo(oaphType, field)
            {
                Default = "default!",
                Accessibility = Accessibility.Public,
            }
        );
        _classInfo.InitializeMembers.Add($"{field} = {oaphInitializaMethodName}();");
        _classInfo.HelperMembers.Add(
            new MethodGenerateInfo(oaphType, oaphInitializaMethodName, getMethod.SplitLine())
        );
    }
}
