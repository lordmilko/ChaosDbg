using ChaosLib.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Stores temporary state that has been generated while the debugger is paused.
    /// </summary>
    class CordbPauseContext
    {
        /// <summary>
        /// Gets the <see cref="DynamicFunctionTableCache"/> that is used to improve <see cref="DynamicFunctionTableProvider"/>
        /// <see cref="RUNTIME_FUNCTION"/> entry resolution performance.
        /// </summary>
        public DynamicFunctionTableCache DynamicFunctionTableCache { get; } = new DynamicFunctionTableCache();

        public void Clear()
        {
            DynamicFunctionTableCache.Clear();
        }
    }
}
