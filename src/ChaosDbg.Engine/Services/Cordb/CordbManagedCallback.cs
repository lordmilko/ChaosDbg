using System;
using System.Threading;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbManagedCallback : CorDebugManagedCallback, IDisposable
    {
        public event EventHandler<CorDebugManagedCallbackEventArgs> OnPreEvent;

        private bool disposed;
        private CountdownEvent entranceCount = new CountdownEvent(1);

        protected override HRESULT HandleEvent<T>(EventHandler<T> handler, CorDebugManagedCallbackEventArgs args)
        {
            if (disposed)
                return HRESULT.E_FAIL;

            if (!entranceCount.TryAddCount())
                return HRESULT.E_FAIL;

            try
            {
                OnPreEvent?.Invoke(this, args);

                return base.HandleEvent(handler, args);
            }
            finally
            {
                entranceCount.Signal();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            //We need to ensure that the callback is not dispatching any events before we allow Dispose to end

            disposed = true;

            entranceCount.Signal();
            entranceCount.Wait();

            entranceCount.Dispose();
        }
    }
}
