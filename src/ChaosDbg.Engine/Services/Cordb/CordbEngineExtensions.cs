namespace ChaosDbg.Cordb
{
    public static class CordbEngineExtensions
    {
        public static void Launch(this CordbEngine engine, string processName, bool startMinimized = false)
        {
            engine.Launch(new DbgLaunchInfo(processName)
            {
                StartMinimized = startMinimized
            });
        }
    }
}
