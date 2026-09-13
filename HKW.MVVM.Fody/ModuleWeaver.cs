// Copyright (c) 2023 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace HKW.MVVM.Fody;

/// <summary>
/// ReactiveUI module weaver.
/// </summary>
/// <seealso cref="BaseModuleWeaver" />
public class ModuleWeaver : BaseModuleWeaver
{
    internal static ModuleWeaverLogger Logger { get; private set; } = null!;

    /// <inheritdoc/>
    public override void Execute()
    {
        //Debugger.Launch();
        Logger = new ModuleWeaverLogger(this, false);

        if (WeaverHelper.Initialize(ModuleDefinition, Logger) is false)
            return;

        // 筛选所有实现了IReactiveObject接口的类
        var classArray = ModuleDefinition
            .GetAllTypes()
            .Where(x =>
                x.BaseType is not null && WeaverHelper.ObservableObject.IsAssignableFrom(x.BaseType)
            )
            .ToArray();
        foreach (var classType in classArray)
        {
            var classInfo = new ClassInfo(classType);
            ReactivePropertyWeaver.Weave(classInfo);
            //NotifyPropertyChangeFromWeaver.Weave(classInfo);
            //ObservableAsPropertyWeaver.Weave(classInfo);
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetAssembliesForScanning()
    {
        return
        [
            "mscorlib",
            "netstandard",
            "System",
            "System.Runtime",
            "CommunityToolkit.Mvvm",
            "HKW.MVVM.SourceGenerator",
        ];
    }
}
