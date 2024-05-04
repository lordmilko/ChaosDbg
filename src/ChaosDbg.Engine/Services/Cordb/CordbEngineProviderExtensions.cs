﻿using System;
using ChaosDbg.Metadata;

namespace ChaosDbg.Cordb
{
    public static class CordbEngineProviderExtensions
    {
        public static ICordbEngine CreateProcess(
            this CordbEngineProvider engineProvider,
            string commandLine,
            bool startMinimized = false,
            bool useInterop = false,
            FrameworkKind? frameworkKind = null,
            Action<ICordbEngine> initCallback = null)
        {
            return engineProvider.CreateProcess(
                new LaunchTargetOptions(commandLine)
                {
                    StartMinimized = startMinimized,
                    UseInterop = useInterop,
                    FrameworkKind = frameworkKind
                },
                initCallback: initCallback
            );
        }

        public static ICordbEngine Attach(
            this CordbEngineProvider engineProvider,
            int processId,
            bool useInterop = false)
        {
            return engineProvider.Attach(
                new LaunchTargetOptions(processId)
                {
                    UseInterop = useInterop
                }
            );
        }
    }
}
