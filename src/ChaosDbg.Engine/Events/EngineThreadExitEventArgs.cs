using System;

namespace ChaosDbg
{
    public class EngineThreadExitEventArgs : EventArgs
    {
        public IDbgThread Thread { get; }

        public object UserContext { get; }

        public EngineThreadExitEventArgs(IDbgThread thread, object userContext = null)
        {
            Thread = thread;
            UserContext = userContext;
        }
    }
}
