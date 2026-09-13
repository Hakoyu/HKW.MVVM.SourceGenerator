// Copyright (c) 2023 .NET Foundation and C
// ontributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HKW.MVVM.Fody;

internal class ModuleWeaverLogger(ModuleWeaver moduleWeaver, bool noInfo = false)
{
    private readonly Action<string> _logInfo = moduleWeaver.WriteInfo;
    private readonly Action<string> _logWarning = moduleWeaver.WriteWarning;
    private readonly Action<string> _logError = moduleWeaver.WriteError;
    private readonly bool _noInfo = noInfo;

    public void LogInfo(string message)
    {
        try
        {
            if (_noInfo)
                _logWarning?.Invoke(message);
            else
                _logInfo?.Invoke(message);
        }
        catch { }
    }

    public void LogWarning(string message)
    {
        _logWarning?.Invoke(message);
    }

    public void LogError(string message)
    {
        _logError?.Invoke(message);
    }
}
