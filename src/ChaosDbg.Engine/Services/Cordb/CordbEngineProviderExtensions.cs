using ChaosDbg.Metadata;

namespace ChaosDbg.Cordb
{
    public static class CordbEngineProviderExtensions
    {
        public static ICordbEngine CreateProcess(
            this CordbEngineProvider engineProvider,
            string commandLine,
            bool startMinimized = false,
            bool useInterop = false,
            ExeKind? exeKind = null)
        {
            return engineProvider.CreateProcess(
                new CreateProcessOptions(commandLine)
                {
                    StartMinimized = startMinimized,
                    UseInterop = useInterop,
                    ExeKind = exeKind
                }
            );
        }

        public static ICordbEngine Attach(
            this CordbEngineProvider engineProvider,
            int processId,
            bool useInterop = false)
        {
            return engineProvider.Attach(
                new AttachProcessOptions(processId)
                {
                    UseInterop = useInterop
                }
            );
        }
    }
}
