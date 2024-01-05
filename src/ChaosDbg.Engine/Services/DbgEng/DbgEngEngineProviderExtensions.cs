using System;

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
            return engineProvider.CreateProcess(new CreateProcessOptions(commandLine)
            {
                StartMinimized = startMinimized
            }, initCallback: initCallback);
        }

        public static DbgEngEngine Attach(
            this DbgEngEngineProvider engineProvider,
            int processId,
            bool nonInvasive = false)
        {
            return engineProvider.Attach(new AttachProcessOptions(processId)
            {
                NonInvasive = nonInvasive
            });
        }
    }
}
