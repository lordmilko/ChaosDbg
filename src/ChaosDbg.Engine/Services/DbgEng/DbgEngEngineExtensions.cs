namespace ChaosDbg.DbgEng
{
    static class DbgEngEngineExtensions
    {
        public static void Launch(this DbgEngEngine engine, string processName, bool startMinimized = false)
        {
            engine.Launch(new DbgEngLaunchInfo(processName)
            {
                StartMinimized = startMinimized
            });
        }
    }
}
