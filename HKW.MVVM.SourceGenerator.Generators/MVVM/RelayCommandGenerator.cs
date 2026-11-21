// Source from https://github.com/SparkyTD/RelayCommand.SourceGenerator

using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal sealed class RelayCommandGenerator
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
        for (var i = 0; i < _classInfo.Methods.Count; i++)
        {
            var methodSymbol = _classInfo.Methods[i];
            AnalyzeMethod(methodSymbol);
        }
    }

    private void AnalyzeMethod(IMethodSymbol methodSymbol)
    {
        // 获取特性数据
        if (
            methodSymbol.TryGetFirstAttribute(
                MVVMGenerator.RelayCommandAttribute,
                out var attributeData
            )
            is false
        )
            return;
        // 参数太多, 提示异常
        if (methodSymbol.Parameters.Length > 1)
        {
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.RelayCommandParametersGreaterThan1,
                methodSymbol.Locations[0]
            );
            _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
            return;
        }
        // 获取特性的参数
        var attributeInfo = attributeData.GetInfo()!;

        // 是否为异步方法
        bool isTask = methodSymbol.ReturnType.InheritedFrom(GeneratorHelper.TaskTypeFullName);
        // 是否为空返回值
        var isReturnTypeVoid = methodSymbol.ReturnType.IsSpecialType(SpecialType.System_Void);

        GeneratorCommand(
            new(
                methodSymbol.Name,
                isReturnTypeVoid ? null : methodSymbol.ReturnType,
                methodSymbol.Parameters.SingleOrDefault()?.Type,
                isTask,
                attributeInfo
            )
        );
    }

    private void GeneratorCommand(RelayCommandInfo commandInfo)
    {
        var fieldName = $"_{commandInfo.MethodName.FirstLetterToLower()}Command";
        var propretyName = $"{commandInfo.MethodName}Command";
        var (commandType, commandCTOR) = GeneratorCommandType(commandInfo);

        var commandField = new FieldGenerateInfo(commandType, fieldName)
        {
            Default = "default!",
            Accessibility = Accessibility.Public,
        };
        var commandProperty = new PropertyGenerateInfo(
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
        _classInfo.HelperMembers.Add(commandField);
        _classInfo.Members.Add(commandProperty);
        if (commandInfo.AttributeInfo.TryGetParam<string>("CanExecute", out var canExecuteProperty))
        {
            if (
                _classInfo.PropertyByName.TryGetValue(canExecuteProperty, out var property) is false
            )
            {
                var diagnostic = Diagnostic.Create(
                    MVVMDescriptors.RelayCommandCanExecutePropertyNotExist,
                    commandInfo.AttributeInfo.Data.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                        commandInfo.AttributeInfo.Data.ApplicationSyntaxReference.Span
                    ),
                    property.Name
                );
                _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
                return;
            }
            // 如果指定的CanExecute属性不是可观察属性, 则诊断
            if (
                property
                    .GetAttributes()
                    .Any(x =>
                        x.AttributeClass!.IsName(MVVMGenerator.ObservablePropertyAttribute)
                        || x.AttributeClass!.IsName(MVVMGenerator.NotifyPropertyChangeFromAttribute)
                        || x.AttributeClass!.IsName(MVVMGenerator.NotifyPropertyChangedForAttribute)
                    )
                is false
            )
            {
                var diagnostic = Diagnostic.Create(
                    MVVMDescriptors.RelayCommandCanExecutePropertyNotObservable,
                    commandInfo.AttributeInfo.Data.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                        commandInfo.AttributeInfo.Data.ApplicationSyntaxReference.Span
                    ),
                    property.Name
                );
                _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
                return;
            }
            if (
                _classInfo.PropertyChangedMemberByName.TryGetValue(
                    canExecuteProperty,
                    out var datas
                )
                is false
            )
            {
                datas = _classInfo.PropertyChangedMemberByName[canExecuteProperty] = new();
            }
            // 为注册的属性添加CanExecute通知方法
            datas.Add($"{fieldName}.NotifyCanExecuteChanged();");
        }
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

        // 如果有CanExecute则添加
        if (commandInfo.AttributeInfo.TryGetParam<string>("CanExecute", out var canExecuteProperty))
        {
            sb.Append($",() => {canExecuteProperty}");
        }
        sb.Append("));");
        return (commandTypeName, sb.ToString());
    }
}
