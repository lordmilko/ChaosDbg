using System;

namespace ChaosDbg
{
    public class EngineInitializedEventArgs : EventArgs
    {
        public IDbgSessionInfo Session { get; }

        public EngineInitializedEventArgs(IDbgSessionInfo session)
        {
            Session = session;
        }
    }
}
