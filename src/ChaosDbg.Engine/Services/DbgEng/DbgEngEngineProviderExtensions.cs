﻿using System;

namespace ChaosDbg.DbgEng
{
    public static class DbgEngEngineProviderExtensions
    {
        public static DbgEngEngine CreateProcess(
            this DbgEngEngineProvider engineProvider,
            string commandLine,
            bool startMinimized = false,
            Action<DbgEngEngine> initCallback = null)
        {
            return engineProvider.CreateProcess(new CreateProcessTargetOptions(commandLine)
            {
                StartMinimized = startMinimized
            }, initCallback: initCallback);
        }

        public static DbgEngEngine Attach(
            this DbgEngEngineProvider engineProvider,
            int processId,
            bool nonInvasive = false,
            bool noSuspend = false,
            bool useDbgEngSymOpts = true,
            int? dbgEngEngineId = null)
        {
            return engineProvider.Attach(new AttachProcessTargetOptions(processId)
            {
                NonInvasive = nonInvasive,
                NoSuspend = noSuspend,
                UseDbgEngSymOpts = useDbgEngSymOpts,
                DbgEngEngineId = dbgEngEngineId
            });
        }

        public static DbgEngEngine OpenDump(
            this DbgEngEngineProvider engineProvider,
            string dumpFile)
        {
            return engineProvider.OpenDump(new OpenDumpTargetOptions(dumpFile));
        }
    }
}
