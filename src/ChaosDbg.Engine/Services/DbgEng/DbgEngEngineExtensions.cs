namespace ChaosDbg.DbgEng
{
    public static class DbgEngEngineExtensions
    {
        public static void Launch(this DbgEngEngine engine, string processName, bool startMinimized = false)
        {
            engine.Launch(new DbgLaunchInfo(processName)
            {
                StartMinimized = startMinimized
            });
        }
    }
}
