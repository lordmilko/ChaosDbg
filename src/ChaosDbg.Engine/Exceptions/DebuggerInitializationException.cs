using ClrDebug;

namespace ChaosDbg
{
    class DebuggerInitializationException : DebugException
    {
        public DebuggerInitializationException(string message, HRESULT hr) : base(message, hr)
        {
        }
    }
}
