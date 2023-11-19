using System;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbManagedCallback : CorDebugManagedCallback
    {
        public EventHandler<CorDebugManagedCallbackEventArgs> OnPreEvent;

        protected override HRESULT HandleEvent<T>(EventHandler<T> handler, CorDebugManagedCallbackEventArgs args)
        {
            OnPreEvent?.Invoke(this, args);

            return base.HandleEvent(handler, args);
        }
    }
}
