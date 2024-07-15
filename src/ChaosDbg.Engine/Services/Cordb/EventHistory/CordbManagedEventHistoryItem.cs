using System.Diagnostics;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    [DebuggerDisplay("[{EventType}] {DebuggerDisplay,nq}")]
    class CordbManagedEventHistoryItem : CordbEventHistoryItem
    {
        protected virtual string DebuggerDisplay => ManagedEventKind.ToString();

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

        protected CordbManagedEventHistoryItem(CorDebugManagedCallbackKind managedEventKind) : base(CordbEventHistoryType.ManagedEvent)
        {
            ManagedEventKind = managedEventKind;
        }

        public override string ToString() => DebuggerDisplay;
    }
}
