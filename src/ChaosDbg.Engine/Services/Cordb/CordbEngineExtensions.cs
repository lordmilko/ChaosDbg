﻿namespace ChaosDbg.Cordb
{
    public static class CordbEngineExtensions
    {
        public static void CreateProcess(this ICordbEngine engine, string commandLine, bool startMinimized = false)
        {
            engine.CreateProcess(
                new CreateProcessOptions(commandLine)
                {
                    StartMinimized = startMinimized
                }
            );
        }

        public static void Attach(this CordbEngine engine, int processId)
        {
            engine.Attach(
                new AttachProcessOptions(processId)
            );
        }
    }
}
