using System;
using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a container that stores temporary values during the lifetime of a managed or unmanaged callback.<para/>
    /// Using this container it is possible to pass values between callback event handlers, or decide whether to perform
    /// a default action based on whether or not a callback event handler performed a more specific action (such as deciding
    /// what to log)
    /// </summary>
    class CordbCallbackContext
    {
        private int initialHistoryCount;
        private CordbPauseReason initialManagedLastStopReason;
        private CordbPauseReason initialUnmanagedLastStopReason;

        private CordbSessionInfo session;

        public CordbCallbackContext(CordbSessionInfo session)
        {
            this.session = session;
        }

        /// <summary>
        /// Gets whether a default <see cref="ICordbEventHistoryItem"/> should be added to <see cref="CordbSessionInfo.EventHistory"/> at the end of the callback.
        /// </summary>
        public bool NeedHistory => session.EventHistory.Count == initialHistoryCount;

        #region Interop
        #region UnmanagedEventProcessId

        private int unmanagedEventProcessId = -1;

        public int UnmanagedEventProcessId
        {
            get
            {
                if (unmanagedEventProcessId == -1)
                    throw new InvalidOperationException($"{nameof(UnmanagedEventProcessId)} has not been set");

                return unmanagedEventProcessId;
            }
            set => unmanagedEventProcessId = value;
        }

        #endregion
        #region UnmanagedEventThreadId

        private int unmanagedEventThreadId = -1;

        public int UnmanagedEventThreadId
        {
            get
            {
                if (unmanagedEventThreadId == -1)
                    throw new InvalidOperationException($"{nameof(UnmanagedEventThreadId)} has not been set");

                return unmanagedEventThreadId;
            }
            set => unmanagedEventThreadId = value;
        }

        #endregion
        #region UnmanagedOutOfBand

        private bool? unmanagedOutOfBand;

        public bool UnmanagedOutOfBand
        {
            get
            {
                if (unmanagedOutOfBand == null)
                    throw new InvalidOperationException($"{nameof(UnmanagedOutOfBand)} has not been set");

                return unmanagedOutOfBand.Value;
            }
            set => unmanagedOutOfBand = value;
        }

        #endregion

        #region UnmanagedContinue

        private bool? unmanagedContinue;

        public bool UnmanagedContinue
        {
            get
            {
                if (unmanagedContinue == null)
                    throw new InvalidOperationException($"{nameof(UnmanagedContinue)} has not been set");

                return unmanagedContinue.Value;
            }
            set => unmanagedContinue = value;
        }

        #endregion
        #region UnmanagedEventType

        private DebugEventType? unmanagedEventType;

        public DebugEventType UnmanagedEventType
        {
            get
            {
                if (unmanagedEventType == null)
                    throw new InvalidOperationException($"{nameof(UnmanagedEventType)} has not been set");

                return unmanagedEventType.Value;
            }
            set => unmanagedEventType = value;
        }

        #endregion

        public bool HasUnmanagedContinue => unmanagedContinue != null;

        public CordbThread UnmanagedEventThread => session.ActiveProcess.Threads[UnmanagedEventThreadId];

        #endregion

        //You can get a managed event while youre processing an unmanaged event, and vice versa, so we need to prevent these two event types
        //tripping over each other

        public void ClearManaged()
        {
            initialHistoryCount = session.EventHistory.Count;
            initialManagedLastStopReason = session.EventHistory.LastStopReason;
        }

        public void EnsureHasStopReason(bool unmanaged)
        {
            //The managed and unmanaged event callbacks may be running concurrently, so we need to keep track of
            //whether we're on the unmanaged event thread or not. If it's a user break, we assume it's the managed event thread,
            //since unmanaegd events can still occur after we've done a managed stop
            var previousReason = unmanaged ? initialUnmanagedLastStopReason : initialManagedLastStopReason;

            if (session.EventHistory.LastStopReason == previousReason)
            {
                Debug.Assert(false, "Attempted to stop the debugger without specifying a stop reason");
                throw new NotImplementedException();
            }
        }

        public void ClearUnmanaged()
        {
            //Interop
            initialUnmanagedLastStopReason = session.EventHistory.LastStopReason;
            unmanagedEventProcessId = -1;
            unmanagedEventThreadId = -1;
            unmanagedOutOfBand = null;
            unmanagedContinue = null;
            unmanagedEventType = null;
        }
    }
}
