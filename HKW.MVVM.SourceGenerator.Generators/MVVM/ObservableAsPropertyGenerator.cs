using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal sealed class ObservableAsPropertyGenerator
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
        foreach (var property in _classInfo.PropertyByName.Values)
        {
            AnalyzeProperty(property);
        }
    }

    private void AnalyzeProperty(IPropertySymbol propertySymbol)
    {
        if (
            propertySymbol.GetFirstAttribute(MVVMGenerator.ObservableAsPropertyAttribute)
            is not AttributeData att
        )
            return;
        // 如果有Set方法则异常
        if (propertySymbol.SetMethod is not null)
        {
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.AttributePropertyHasSetMethod,
                propertySymbol.Locations[0],
                nameof(MVVMGenerator.ObservableAsPropertyAttribute)
            );
            _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
        if (propertySymbol.TryGetGetMethodContent(out var getMethod) is false)
            return;
        // 如果不是ToProperty方法则取消
        if (getMethod.Contains(".ToProperty(") is false)
        {
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.ObservableAsPropertyStructuralError,
                att.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                    att.ApplicationSyntaxReference.Span
                )
            );
            _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
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
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.ObservableAsPropertyStructuralError,
                att.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                    att.ApplicationSyntaxReference.Span
                )
            );
            _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
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
