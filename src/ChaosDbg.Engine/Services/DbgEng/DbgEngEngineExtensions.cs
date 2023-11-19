namespace ChaosDbg.DbgEng
{
    public static class DbgEngEngineExtensions
    {
        public static void Launch(this DbgEngEngine engine, string commandLine, bool startMinimized = false)
        {
            engine.Launch(new DbgLaunchInfo(commandLine)
            {
                StartMinimized = startMinimized
            });
        }
    }
}
