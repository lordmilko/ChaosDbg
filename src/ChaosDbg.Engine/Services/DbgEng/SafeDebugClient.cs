using System;
using System.Threading;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Encapsulates a <see cref="DebugClient"/> and prevents it from being used outside of the thread that created it.
    /// </summary>
    class SafeDebugClient : IDisposable
    {
        private bool disposed;

        public DebugClient Client
        {
            get
            {
                CheckIfDisposed();

                if (threadId != Thread.CurrentThread.ManagedThreadId)
                    throw new InvalidOperationException($"Attempted to access DebugClient {threadId} from thread {Thread.CurrentThread.ManagedThreadId}");

                return client;
            }
        }

        private DebugClient client;
        private int threadId;

        public SafeDebugClient(DebugClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            this.client = client;
            threadId = Thread.CurrentThread.ManagedThreadId;
        }

        public DebugClient UnsafeGetClient() => client;

        private void CheckIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(SafeDebugClient));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            client?.Dispose();
            client = null;
        }
    }
}
