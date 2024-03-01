using System;

namespace ChaosDbg
{
    public class EngineThreadCreateEventArgs : EventArgs
    {
        public  IDbgThread Thread { get; }

        public object UserContext { get; }

        public EngineThreadCreateEventArgs(IDbgThread thread, object userContext = null)
        {
            Thread = thread;
            UserContext = userContext;
        }
    }
}
