using System;
using ClrDebug;

namespace ChaosDbg
{
    public class EngineExceptionHitEventArgs : EventArgs
    {
        public EXCEPTION_DEBUG_INFO Exception { get; }

        public EngineExceptionHitEventArgs(in EXCEPTION_DEBUG_INFO exception)
        {
            Exception = exception;
        }
    }
}
