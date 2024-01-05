using System;
using System.Collections.Generic;
using System.Threading;
using ChaosDbg.Cordb;

namespace ChaosDbg
{
    public abstract class DebugEngineProvider<T> where T : IDbgEngine
    {
        private bool disposed;

        private List<T> engines = new List<T>();

        private object objLock = new object();

        /// <summary>
        /// Creates a new <typeparamref name="T"/> against a newly created process.
        /// </summary>
        /// <param name="options">The options to use to create the process.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to shutdown the engine thread.</param>
        /// <param name="initCallback">A callback that is used to initialize the newly created engine prior to launching the target process.</param>
        /// <returns>An <see cref="ICordbEngine"/> for debugging the specified process.</returns>
        public T CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default, Action<T> initCallback = null)
        {
            CheckIfDisposed();

            var engine = CreateEngine();

            initCallback?.Invoke(engine);

            ((IDbgEngineInternal) engine).CreateProcess(options, cancellationToken);

            return engine;
        }

        public T Attach(AttachProcessOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();

            var engine = CreateEngine();

            ((IDbgEngineInternal) engine).Attach(options, cancellationToken);

            return engine;
        }

        private T CreateEngine()
        {
            lock (objLock)
            {
                var engine = NewEngine();

                engines.Add(engine);

                return engine;
            }
        }

        protected abstract T NewEngine();

        private void CheckIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(CordbEngineProvider));
        }

        public void Dispose()
        {
            if (disposed)
                return;

            lock (objLock)
            {
                foreach (var engine in engines)
                    engine.Dispose();
            }

            disposed = true;
        }
    }
}
