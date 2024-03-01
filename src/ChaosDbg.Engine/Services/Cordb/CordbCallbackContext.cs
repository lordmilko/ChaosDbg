using System;

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

        public void Clear()
        {
            initialHistoryCount = session.EventHistory.Count;

            //Interop
            unmanagedEventProcessId = -1;
            unmanagedEventThreadId = -1;
            unmanagedOutOfBand = null;
        }
    }
}
