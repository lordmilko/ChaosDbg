namespace ChaosDbg.DbgEng
{
    public static class DbgEngEngineExtensions
    {
        public static void CreateProcess(
            this DbgEngEngine engine,
            string commandLine,
            bool startMinimized = false)
        {
            engine.CreateProcess(new CreateProcessOptions(commandLine)
            {
                StartMinimized = startMinimized
            });
        }

        public static void Attach(
            this DbgEngEngine engine,
            int processId,
            bool nonInvasive = false)
        {
            engine.Attach(new AttachProcessOptions(processId)
            {
                NonInvasive = nonInvasive
            });
        }
    }
}
