namespace ChaosDbg.Cordb
{
    public static class CordbEngineExtensions
    {
        public static void Launch(this CordbEngine engine, string commandLine, bool startMinimized = false)
        {
            engine.Launch(new DbgLaunchInfo(commandLine)
            {
                StartMinimized = startMinimized
            });
        }
    }
}
