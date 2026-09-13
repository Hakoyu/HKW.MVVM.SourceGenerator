// Source from https://github.com/SparkyTD/RelayCommand.SourceGenerator

using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal class RelayCommandGenerator
{
    public static void Generate(ClassInfo classInfo)
    {
        var g = new RelayCommandGenerator(classInfo);
        g.Execute();
    }

    private readonly ClassInfo _classInfo;

    public RelayCommandGenerator(ClassInfo classInfo)
    {
        _classInfo = classInfo;
    }

    private void Execute()
    {
        for (var i = 0; i < _classInfo.MethodSSs.Count; i++)
        {
            var methodSymbol = _classInfo.MethodSSs[i];
            AnalyzeMethod(methodSymbol);
        }
    }

    private void AnalyzeMethod(MethodSS methodSS)
    {
        methodSS.OutData(out var methodSyntax, out var methodSymbol);

        // 获取特性数据
        if (
            methodSymbol.TryGetFirstAttribute(
                TypeFullNames.RelayCommandAttribute,
                out var attributeData
            )
            is false
        )
            return;
        // 参数太多, 提示异常
        if (methodSymbol.Parameters.Length > 1)
        {
            var diagnostic = Diagnostic.Create(
                Descriptors.RelayCommandParametersGreaterThan1,
                methodSyntax.GetLocation()
            );
            GeneratorHelper.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
        // 获取特性的参数
        var attributeParams = attributeData.GetParams();

        // 是否为异步方法
        bool isTask = methodSymbol.ReturnType.InheritedFromX(
            GeneratorHelper.TaskTypeFullName,
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        // 是否为空返回值
        var isReturnTypeVoid = methodSymbol.ReturnType.IsVoid();

        GeneratorCommand(
            new(
                methodSymbol.Name,
                isReturnTypeVoid ? null : methodSymbol.ReturnType,
                methodSymbol.Parameters.SingleOrDefault()?.Type,
                isTask,
                attributeParams
            )
        );
    }

    private void GeneratorCommand(RelayCommandInfo commandInfo)
    {
        var fieldName = $"_{commandInfo.MethodName.FirstLetterToLower()}Command";
        var propretyName = $"{commandInfo.MethodName}Command";
        var (commandType, commandCTOR) = GeneratorCommandType(commandInfo);

        var field = new FieldGenerateInfo(commandType, fieldName)
        {
            Default = "default!",
            Accessibility = Accessibility.Public,
        };
        var property = new PropertyGenerateInfo(
            commandType,
            propretyName,
            new(
                $"=> {_classInfo.HelperPropertyName}.{fieldName} ?? ({_classInfo.HelperPropertyName}.{fieldName} = {commandCTOR}"
            )
        )
        {
            Comment =
                $"/// <inheritdoc cref=\"{commandInfo.MethodName}({(commandInfo.ArgumentType is null ? string.Empty : commandInfo.ArgumentType?.GetFullName().ReplaceBraces())})\"/>",
            Accessibility = Accessibility.Public,
        };
        _classInfo.HelperMembers.Add(field);
        _classInfo.Members.Add(property);
    }

    private static (string commandType, string commandCTOR) GeneratorCommandType(
        RelayCommandInfo commandInfo
    )
    {
        var commandTypeName = string.Empty;
        var inputType = commandInfo.ArgumentType?.GetFullName() ?? string.Empty;
        if (commandInfo.IsTask)
        {
            commandTypeName = commandInfo.ArgumentType is null
                ? $"global::CommunityToolkit.Mvvm.Input.AsyncRelayCommand"
                : $"global::CommunityToolkit.Mvvm.Input.AsyncRelayCommand<{inputType}>";
        }
        else
        {
            commandTypeName = commandInfo.ArgumentType is null
                ? $"global::CommunityToolkit.Mvvm.Input.RelayCommand"
                : $"global::CommunityToolkit.Mvvm.Input.RelayCommand<{inputType}>";
        }

        var sb = new StringBuilder();
        sb.Append($"new(");
        // 检测异步和参数
        if (commandInfo.ArgumentType is null)
        {
            sb.Append(commandInfo.MethodName);
        }
        else
        {
            sb.Append(
                commandInfo.IsTask ? commandInfo.MethodName : $"x=>{commandInfo.MethodName}(x)"
            );
        }

        // 如果有CanExecute则添加canExecute参数
        if (
            commandInfo.Attributes.TryGetParam<string>("CanExecute", out var canExecutePropertyName)
        )
        {
            sb.Append($",() => {canExecutePropertyName}");
        }
        sb.Append("));");
        return (commandTypeName, sb.ToString());
    }
}
