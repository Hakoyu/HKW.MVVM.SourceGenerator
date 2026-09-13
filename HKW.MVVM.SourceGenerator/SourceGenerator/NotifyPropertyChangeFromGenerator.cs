using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal class NotifyPropertyChangeFromGenerator
{
    public static void Generate(ClassInfo classInfo)
    {
        var analyzer = new NotifyPropertyChangeFromGenerator(classInfo);
        analyzer.Process();
    }

    public NotifyPropertyChangeFromGenerator(ClassInfo classInfo)
    {
        _classInfo = classInfo;
    }

    private readonly ClassInfo _classInfo;

    private void Process()
    {
        var infos = new List<NotifyPropertyChangeFromInfo>();
        for (var i = 0; i < _classInfo.PropertySSs.Count; i++)
        {
            var property = _classInfo.PropertySSs[i];
            var info = AnalyzeProperty(property.Symbol);
            if (info is not null)
                infos.Add(info);
        }
        Generate(infos);
    }

    private NotifyPropertyChangeFromInfo? AnalyzeProperty(IPropertySymbol propertySymbol)
    {
        // 获取特性数据
        if (
            propertySymbol.TryGetFirstAttribute(
                TypeFullNames.NotifyPropertyChangeFrom,
                out var attributeData
            )
            is false
        )
            return null;
        // 如果有Set方法则异常
        if (propertySymbol.SetMethod is not null)
        {
            var diagnostic = Diagnostic.Create(
                Descriptors.PropertyHasSetMethod,
                attributeData.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
                    attributeData.ApplicationSyntaxReference.Span
                ),
                nameof(TypeFullNames.NotifyPropertyChangeFrom)
            );
            GeneratorHelper.ProductionContext.ReportDiagnostic(diagnostic);
            return null;
        }
        // 获取特性的参数
        var attributeParameters = attributeData.GetParams();
        if (
            attributeParameters.TryGetParams<string>(
                nameof(NotifyPropertyChangeFromAttribute.PropertyNames),
                out var propertyNames
            )
            is false
        )
            return null;

        if (propertySymbol.TryGetGetMethodContent(out var getMethod) is false)
            return null;
        var info = new NotifyPropertyChangeFromInfo(
            propertySymbol,
            getMethod,
            propertyNames.Distinct().ToArray()
        );
        if (
            attributeParameters.TryGetParam<NotifyPropertyChangeFromCacheMode>(
                nameof(NotifyPropertyChangeFromAttribute.CacheMode),
                out var cacheMode
            )
        )
        {
            info.CacheMode = cacheMode;
        }

        foreach (var param in info.Params)
        {
            if (
                _classInfo.PropertyChangedMemberByName.TryGetValue(param, out var changedMembers)
                is false
            )
                changedMembers = _classInfo.PropertyChangedMemberByName[param] = [];
            if (
                _classInfo.PropertyChangingMemberByName.TryGetValue(param, out var changingMembers)
                is false
            )
                changingMembers = _classInfo.PropertyChangingMemberByName[param] = [];
            changingMembers.Add(info.ChangingMethodName + "();");
            changedMembers.Add(info.ChangedMethodName + "();");
        }
        return info;
    }

    private void Generate(List<NotifyPropertyChangeFromInfo> infos)
    {
        foreach (var info in infos)
        {
            GenerateFromInfo(info);
        }
    }

    private void GenerateFromInfo(NotifyPropertyChangeFromInfo info)
    {
        var changingMethod = new MethodGenerateInfo(
            GeneratorHelper.TypeVoid,
            info.ChangingMethodName,
            GenerateChangingMethodContexts(info)
        );
        var changedMethod = new MethodGenerateInfo(
            GeneratorHelper.TypeVoid,
            info.ChangedMethodName,
            GenerateChangedMethodContexts(info)
        );
        if (info.CacheMode != NotifyPropertyChangeFromCacheMode.Disable)
        {
            // 缓存字段
            var field = new FieldGenerateInfo(
                info.Property.Type,
                $"_{info.Property.Name.FirstLetterToLower()}Cache"
            )
            {
                Accessibility = Accessibility.Public,
                Default = "default!",
            };
            var getMethod = new MethodGenerateInfo(
                info.Property.Type,
                $"Get{info.Property.Name}",
                info.GetMethod
            );
            if (info.CacheMode is NotifyPropertyChangeFromCacheMode.Enable)
                _classInfo.InitializeMembers.Add($"{field.Name} = {getMethod.Name}();");
            _classInfo.HelperMembers.Add(field);
            _classInfo.HelperMembers.Add(getMethod);
        }
        _classInfo.HelperMembers.Add(changingMethod);
        _classInfo.HelperMembers.Add(changedMethod);
    }

    public List<string> GenerateChangingMethodContexts(NotifyPropertyChangeFromInfo info)
    {
        var contents = new List<string>();
        contents.Add($"_source.OnPropertyChanging(\"{info.Property.Name}\");");
        if (
            _classInfo.PropertyChangingMemberByName.TryGetValue(
                info.Property.Name,
                out var changingActions
            )
        )
        {
            contents.Add("");
            foreach (var action in changingActions)
                contents.Add(action);
        }
        return contents;
    }

    public List<string> GenerateChangedMethodContexts(NotifyPropertyChangeFromInfo info)
    {
        var contents = new List<string>();

        contents.Add($"_source.OnPropertyChanged(\"{info.Property.Name}\");");

        if (
            _classInfo.PropertyChangedMemberByName.TryGetValue(
                info.Property.Name,
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
}
