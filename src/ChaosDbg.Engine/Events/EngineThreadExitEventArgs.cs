using System;

namespace ChaosDbg
{
    public class EngineThreadExitEventArgs : EventArgs
    {
        public IDbgThread Thread { get; }

        public EngineThreadExitEventArgs(IDbgThread thread)
        {
            Thread = thread;
        }
    }
}
