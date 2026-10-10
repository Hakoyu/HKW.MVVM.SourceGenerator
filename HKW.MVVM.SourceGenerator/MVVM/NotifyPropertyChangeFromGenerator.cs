using System.Text;
using HKW.SourceGeneratorUtils;
using Microsoft.CodeAnalysis;

namespace HKW.MVVM.SourceGenerator;

internal sealed class NotifyPropertyChangeFromGenerator
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
        var infoByProperty = new Dictionary<IPropertySymbol, NotifyPropertyChangeFromInfo>();
        foreach (var property in _classInfo.PropertyByName.Values)
        {
            var info = AnalyzeProperty(property);
            if (info is not null)
                infoByProperty.Add(property, info);
        }
        AnalyzeForPropertyNames(infoByProperty);

        Generate(infoByProperty);
    }

    private void AnalyzeForPropertyNames(
        Dictionary<IPropertySymbol, NotifyPropertyChangeFromInfo> infoByProperty
    )
    {
        var pairs = infoByProperty.ToArray();
        foreach (var pair in pairs.Where(x => x.Value.ForPropertyNames?.Count > 0))
        {
            foreach (var propertyName in pair.Value.ForPropertyNames)
            {
                var property = _classInfo.PropertyByName[propertyName];

                if (infoByProperty.TryGetValue(property, out var newInfo) is false)
                    newInfo = infoByProperty[property] = new(property, "", []);
                newInfo.PropertyNames.Add(pair.Key.Name);
            }
            pair.Value.ForPropertyNames.Clear();
            pair.Value.ForPropertyNames = null!;
        }
        foreach (var info in infoByProperty.Values)
        {
            foreach (var param in info.PropertyNames)
            {
                if (
                    _classInfo.PropertyChangedMemberByName.TryGetValue(
                        param,
                        out var changedMembers
                    )
                    is false
                )
                    changedMembers = _classInfo.PropertyChangedMemberByName[param] = [];
                if (
                    _classInfo.PropertyChangingMemberByName.TryGetValue(
                        param,
                        out var changingMembers
                    )
                    is false
                )
                    changingMembers = _classInfo.PropertyChangingMemberByName[param] = [];
                changingMembers.Add(info.ChangingMethodName + "();");
                changedMembers.Add(info.ChangedMethodName + "();");
            }
        }
    }

    private NotifyPropertyChangeFromInfo? AnalyzeProperty(IPropertySymbol property)
    {
        var info = AnalyzeNotifyPropertyChangeFrom(property);
        info = AnalyzeNotifyPropertyChangeFor(property, info);
        return info;
    }

    private NotifyPropertyChangeFromInfo? AnalyzeNotifyPropertyChangeFrom(IPropertySymbol property)
    {
        // 获取特性数据
        if (
            property.TryGetFirstAttribute(
                MVVMGenerator.NotifyPropertyChangeFromAttribute,
                out var attributeData
            )
            is false
        )
            return null;
        var attributeLocation = attributeData.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
            attributeData.ApplicationSyntaxReference.Span
        );
        // 如果有Set方法则诊断
        if (property.SetMethod is not null)
        {
            var diagnostic = Diagnostic.Create(
                MVVMDescriptors.AttributePropertyHasSetMethod,
                attributeLocation,
                nameof(MVVMGenerator.NotifyPropertyChangeFromAttribute)
            );
            _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
            return null;
        }
        // 获取特性的参数
        var attributeInfo = attributeData.GetInfo()!;
        var propertyNames = attributeInfo.GetParams<string>("propertyNames");
        {
            if (attributeInfo.TryGetParam<string>("propertyName", out var propertyName))
                propertyNames = [propertyName, .. propertyNames ?? Array.Empty<string>()];
            if (propertyNames is null || propertyNames.Length == 0)
                return null;
        }

        var validPropertyNames = new HashSet<string>();
        foreach (var name in propertyNames)
        {
            if (_classInfo.PropertyByName.ContainsKey(name) is false)
            {
                _classInfo.ProductionContext.ReportDiagnostic(
                    Diagnostic.Create(
                        MVVMDescriptors.NotifyPropertyChangeFromPropertyNotExist,
                        attributeLocation,
                        name
                    )
                );
                continue;
            }
            if (validPropertyNames.Add(name) is false)
            {
                _classInfo.ProductionContext.ReportDiagnostic(
                    Diagnostic.Create(
                        MVVMDescriptors.NotifyPropertyChangeFromDuplicateProperty,
                        attributeLocation,
                        name
                    )
                );
            }
        }

        if (property.TryGetGetMethodContent(out var getMethod) is false)
            return null;
        var info = new NotifyPropertyChangeFromInfo(property, getMethod, validPropertyNames);
        if (
            attributeInfo.TryGetParam<NotifyPropertyChangeFromCacheMode>(
                nameof(NotifyPropertyChangeFromAttribute.CacheMode),
                out var cacheMode
            )
        )
        {
            info.CacheMode = cacheMode;
        }

        return info;
    }

    private NotifyPropertyChangeFromInfo? AnalyzeNotifyPropertyChangeFor(
        IPropertySymbol property,
        NotifyPropertyChangeFromInfo? info
    )
    {
        // 获取特性数据
        if (
            property.TryGetFirstAttribute(
                MVVMGenerator.NotifyPropertyChangedForAttribute,
                out var attributeData
            )
            is false
        )
            return info;
        var attributeLocation = attributeData.ApplicationSyntaxReference?.SyntaxTree.GetLocation(
            attributeData.ApplicationSyntaxReference.Span
        );

        // 获取特性的参数
        var attributeInfo = attributeData.GetInfo()!;
        var propertyNames = attributeInfo.GetParams<string>("propertyNames");
        {
            if (attributeInfo.TryGetParam<string>("propertyName", out var propertyName))
                propertyNames = [propertyName, .. propertyNames ?? Array.Empty<string>()];
            if (propertyNames is null || propertyNames.Length == 0)
                return info;
        }

        var validPropertyNames = new HashSet<string>();
        foreach (var name in propertyNames)
        {
            if (_classInfo.PropertyByName.TryGetValue(name, out var targetProperty) is false)
            {
                _classInfo.ProductionContext.ReportDiagnostic(
                    Diagnostic.Create(
                        MVVMDescriptors.NotifyPropertyChangeFromPropertyNotExist,
                        attributeLocation,
                        name
                    )
                );
                continue;
            }
            // 如果目标属性有Set方法则诊断
            if (targetProperty.SetMethod is not null)
            {
                var diagnostic = Diagnostic.Create(
                    MVVMDescriptors.NotifyPropertyChangeForTargetHasSetMethod,
                    attributeLocation,
                    name
                );
                _classInfo.ProductionContext.ReportDiagnostic(diagnostic);
                continue;
            }
            if (validPropertyNames.Add(name) is false)
            {
                _classInfo.ProductionContext.ReportDiagnostic(
                    Diagnostic.Create(
                        MVVMDescriptors.NotifyPropertyChangeFromDuplicateProperty,
                        attributeLocation,
                        name
                    )
                );
            }
        }
        if (info is null)
        {
            info = new NotifyPropertyChangeFromInfo(property, "", []);
        }
        info.ForPropertyNames = validPropertyNames;
        return info;
    }

    private void Generate(Dictionary<IPropertySymbol, NotifyPropertyChangeFromInfo> infoByProperty)
    {
        foreach (var info in infoByProperty.Values.Where(x => x.PropertyNames.Count > 0))
        {
            GenerateFromInfo(info);
        }
    }

    private void GenerateFromInfo(NotifyPropertyChangeFromInfo info)
    {
        var changingMethod = new MethodGenerateInfo(
            GeneratorHelper.VoidName,
            info.ChangingMethodName,
            GenerateChangingMethodContexts(info)
        );
        var changedMethod = new MethodGenerateInfo(
            GeneratorHelper.VoidName,
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
            var ass = $"{field.Name} = {ClassInfo.SourceName}.{getMethod.Name}();";
            // 如果启用则初始化
            if (info.CacheMode is NotifyPropertyChangeFromCacheMode.Enable)
                _classInfo.InitializeMembers.Add(ass);
            _classInfo.HelperMembers.Add(field);
            _classInfo.Members.Add(getMethod);
            changedMethod.Contents.Insert(0, ass);
        }
        _classInfo.HelperMembers.Add(changingMethod);
        _classInfo.HelperMembers.Add(changedMethod);
    }

    public List<string> GenerateChangingMethodContexts(NotifyPropertyChangeFromInfo info)
    {
        var contents = new List<string>();
        contents.Add(
            $"{ClassInfo.SourceName}.OnPropertyChanging({_classInfo.ChangeArgsCache.GetChangingArgs(info.Property.Name)});"
        );
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
        contents.Add(
            $"{ClassInfo.SourceName}.OnPropertyChanged({_classInfo.ChangeArgsCache.GetChangedArgs(info.Property.Name)});"
        );

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
