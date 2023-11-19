using System;

namespace ChaosDbg
{
    public class EngineThreadCreateEventArgs : EventArgs
    {
        public  IDbgThread Thread { get; }

        public EngineThreadCreateEventArgs(IDbgThread thread)
        {
            Thread = thread;
        }
    }
}
