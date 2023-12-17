using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    [DebuggerDisplay("[{EventType}] {DebuggerDisplay,nq}")]
    class CordbManagedEventHistoryItem : ICordbEventHistoryItem
    {
        protected virtual string DebuggerDisplay => ManagedEventKind.ToString();

        public CordbEventHistoryType EventType => CordbEventHistoryType.ManagedEvent;

        public int EventThread { get; }

        public CorDebugManagedCallbackKind ManagedEventKind { get; }

        public static CordbManagedEventHistoryItem New(CorDebugManagedCallbackEventArgs e)
        {
            switch (e.Kind)
            {
                case CorDebugManagedCallbackKind.LoadModule:
                    return new CordbManagedModuleLoadEventHistoryItem((LoadModuleCorDebugManagedCallbackEventArgs) e);

                case CorDebugManagedCallbackKind.UnloadModule:
                    return new CordbManagedModuleUnloadEventHistoryItem((UnloadModuleCorDebugManagedCallbackEventArgs) e);

                case CorDebugManagedCallbackKind.CreateThread:
                    return new CordbManagedThreadCreateEventHistoryItem((CreateThreadCorDebugManagedCallbackEventArgs) e);

                case CorDebugManagedCallbackKind.ExitThread:
                    return new CordbManagedThreadExitEventHistoryItem((ExitThreadCorDebugManagedCallbackEventArgs) e);

                default:
                    return new CordbManagedEventHistoryItem(e.Kind);
            }
        }

        protected CordbManagedEventHistoryItem(CorDebugManagedCallbackKind managedEventKind)
        {
            ManagedEventKind = managedEventKind;
            EventThread = Kernel32.GetCurrentThreadId();
        }

        public override string ToString() => DebuggerDisplay;
    }
}
