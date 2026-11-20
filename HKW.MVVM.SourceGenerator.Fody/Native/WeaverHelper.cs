using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace HKW.MVVM.SourceGenerator.Fody;

internal static class WeaverHelper
{
    public static bool Initialize(
        ModuleDefinition moduleDefinition,
        ModuleWeaverLogger logger,
        Func<string, TypeDefinition> findTypeDefinition
    )
    {
        ModuleDefinition = moduleDefinition;
        Logger = logger;

        MVVMToolkit = moduleDefinition
            .AssemblyReferences.Where(x => x.Name == "CommunityToolkit.Mvvm")
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();
        if (MVVMToolkit is null)
        {
            Logger.LogError(
                $"Could not find assembly: CommunityToolkit.Mvvm in (\"{moduleDefinition.Name}\")"
            );
            return false;
        }
        var toolkitAssembly = moduleDefinition.AssemblyResolver.Resolve(MVVMToolkit);

        ObservableObject =
            toolkitAssembly.MainModule.GetType(
                "CommunityToolkit.Mvvm.ComponentModel.ObservableObject"
            )
            ?? throw new WeaverException(
                $"Could not find CommunityToolkit.Mvvm.ComponentModel.ObservableObject in {toolkitAssembly.Name.Name}."
            );

        Logger.LogInfo($"{MVVMToolkit.Name} {MVVMToolkit.Version}");

        if (moduleDefinition.Assembly.Name.Name == "HKW.MVVM.SourceGenerator")
        {
            HKWMVVMSourceGenerator = moduleDefinition.Assembly.Name;
        }
        else
        {
            HKWMVVMSourceGenerator = moduleDefinition
                .AssemblyReferences.Where(x => x.Name == "HKW.MVVM.SourceGenerator")
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
            if (HKWMVVMSourceGenerator is null)
            {
                Logger.LogError(
                    "Could not find assembly: HKW.MVVM.SourceGenerator ("
                        + string.Join(", ", moduleDefinition.AssemblyReferences.Select(x => x.Name))
                        + ")"
                );
            }
        }
        Logger.LogInfo($"{HKWMVVMSourceGenerator!.Name} {HKWMVVMSourceGenerator.Version}");

        ObservablePropertyAttribute =
            ModuleDefinition.FindType(
                "CommunityToolkit.Mvvm.ComponentModel",
                "ObservablePropertyAttribute",
                HKWMVVMSourceGenerator
            ) ?? throw new WeaverException("ObservablePropertyAttribute is null");

        NotifyPropertyChangeFromAttribute =
            ModuleDefinition.FindType(
                "HKW.MVVM.SourceGenerator",
                "NotifyPropertyChangeFromAttribute",
                HKWMVVMSourceGenerator
            ) ?? throw new WeaverException("NotifyPropertyChangeFromAttribute is null");

        HKWMVVM = moduleDefinition
            .AssemblyReferences.Where(x => x.Name == "HKW.MVVM")
            .OrderByDescending(x => x.Version)
            .FirstOrDefault();

        ObservableAsPropertyAttribute = ModuleDefinition.FindType(
            "HKW.MVVM.SourceGenerator",
            "ObservableAsPropertyAttribute",
            HKWMVVMSourceGenerator
        );

        ObservableAsPropertyHelper = ModuleDefinition.FindType(
            "HKW.MVVM",
            "ObservableAsPropertyHelper`1",
            HKWMVVM,
            "T"
        );

        InitializeGeneratedCodeAttribute(ModuleDefinition, findTypeDefinition);
        return true;
    }

    private static void InitializeGeneratedCodeAttribute(
        ModuleDefinition moduleDefinition,
        Func<string, TypeDefinition> findTypeDefinition
    )
    {
        // Resolve against the target framework, not the build host's runtime assemblies.
        var generatedCodeType = findTypeDefinition("System.CodeDom.Compiler.GeneratedCodeAttribute");
        var debuggerBrowsableType = findTypeDefinition("System.Diagnostics.DebuggerBrowsableAttribute");
        var generatedCodeConstructor = moduleDefinition.ImportReference(
            generatedCodeType.Methods.Single(x =>
                x.IsConstructor
                && x.IsStatic is false
                && x.Parameters.Count == 2
                && x.Parameters.All(p => p.ParameterType.FullName == "System.String")
            )
        );
        var debuggerBrowsableConstructor = moduleDefinition.ImportReference(
            debuggerBrowsableType.Methods.Single(x =>
                x.IsConstructor
                && x.IsStatic is false
                && x.Parameters.Count == 1
                && x.Parameters[0].ParameterType.FullName == "System.Diagnostics.DebuggerBrowsableState"
            )
        );

        GeneratedCodeAttribute = new CustomAttribute(generatedCodeConstructor);
        GeneratedCodeAttribute.ConstructorArguments.Add(
            new CustomAttributeArgument(
                moduleDefinition.TypeSystem.String,
                HKWMVVMSourceGenerator.Name
            )
        );
        GeneratedCodeAttribute.ConstructorArguments.Add(
            new CustomAttributeArgument(
                moduleDefinition.TypeSystem.String,
                HKWMVVMSourceGenerator.Version?.ToString() ?? string.Empty
            )
        );

        DebuggerBrowsableAttribute = new CustomAttribute(debuggerBrowsableConstructor);
        DebuggerBrowsableAttribute.ConstructorArguments.Add(
            new CustomAttributeArgument(
                debuggerBrowsableConstructor.Parameters[0].ParameterType,
                (int)DebuggerBrowsableState.Never
            )
        );
    }

    private static CustomAttribute? GeneratedCodeAttribute { get; set; }
    private static CustomAttribute? DebuggerBrowsableAttribute { get; set; }

    public static void AddGeneratedCodeAttribute(FieldDefinition field)
    {
        field.CustomAttributes.Add(GeneratedCodeAttribute);
        field.CustomAttributes.Add(DebuggerBrowsableAttribute);
    }

    public static ModuleDefinition ModuleDefinition { get; private set; } = null!;
    public static ModuleWeaverLogger Logger { get; private set; } = null!;
    public static AssemblyNameReference MVVMToolkit { get; private set; } = null!;
    public static AssemblyNameReference? HKWMVVM { get; private set; } = null!;
    public static AssemblyNameReference HKWMVVMSourceGenerator { get; private set; } = null!;
    public static TypeDefinition ObservableObject { get; private set; } = null!;
    public static TypeReference ObservablePropertyAttribute { get; private set; } = null!;
    public static TypeReference NotifyPropertyChangeFromAttribute { get; private set; } = null!;
    public static TypeReference ObservableAsPropertyAttribute { get; private set; } = null!;
    public static TypeReference ObservableAsPropertyHelper { get; private set; } = null!;
}
