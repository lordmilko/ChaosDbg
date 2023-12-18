using ChaosDbg.Metadata;

namespace ChaosDbg.Cordb
{
    public static class CordbEngineExtensions
    {
        public static void CreateProcess(
            this ICordbEngine engine,
            string commandLine,
            bool startMinimized = false,
            bool useInterop = false,
            ExeKind? exeKind = null)
        {
            engine.CreateProcess(
                new CreateProcessOptions(commandLine)
                {
                    StartMinimized = startMinimized,
                    UseInterop = useInterop,
                    ExeKind = exeKind
                }
            );
        }

        public static void Attach(this CordbEngine engine, int processId, bool useInterop = false)
        {
            engine.Attach(
                new AttachProcessOptions(processId)
                {
                    UseInterop = useInterop
                }
            );
        }
    }
}
