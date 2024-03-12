using ChaosLib;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides information about why the normal debugger loop was stopped to prompt for user input.
    /// </summary>
    public abstract class CordbPauseReason : ICordbEventHistoryItem
    {
        #region ICordbEventHistoryItem

        public CordbEventHistoryType EventType { get; }
        
        public int EventThread { get; }

        #endregion

        protected CordbPauseReason(CordbEventHistoryType eventType)
        {
            EventType = eventType;
            EventThread = Kernel32.GetCurrentThreadId();
        }
    }
}
