using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using ChaosDbg.Cordb;

namespace ChaosDbg
{
    public abstract class DebugEngineProvider<TEngine> where TEngine : IDbgEngine
    {
        private bool disposed;

        private List<TEngine> engines = new List<TEngine>();

        private object objLock = new object();

        private TEngine activeEngine;

        public TEngine ActiveEngine
        {
            get
            {
                if (activeEngine != null)
                    return activeEngine;

                lock (objLock)
                {
                    activeEngine = engines.FirstOrDefault();

                    return activeEngine;
                }
            }
        }

        #region Events

        /* Debug Engine Providers do not participate in event handling. All events that are bound to a debug engine provider
         * are immediately relayed to the engines that it is currently managing. Active events are also cached, so that
         * in the event a new engine is created, it can immediately be brought up to speed with all of the event handlers
         * that it should be participating in */

        protected readonly EventHandlerList events = new EventHandlerList();

        /// <summary>
        /// The event that occurs when the engine wishes to print output to the console.
        /// </summary>
        public event EventHandler<EngineOutputEventArgs> EngineOutput
        {
            add => events.AddHandler(nameof(EngineOutput), value);
            remove => events.RemoveHandler(nameof(EngineOutput), value);
        }

        /// <summary>
        /// The event that occurs when the debugger status changes (e.g. from broken to running).
        /// </summary>
        public event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged
        {
            add => events.AddHandler(nameof(EngineStatusChanged), value);
            remove => events.RemoveHandler(nameof(EngineStatusChanged), value);
        }

        /// <summary>
        /// The event that occurs when a module is loaded into the current process.
        /// </summary>
        public event EventHandler<EngineModuleLoadEventArgs> ModuleLoad
        {
            add => events.AddHandler(nameof(ModuleLoad), value);
            remove => events.RemoveHandler(nameof(ModuleLoad), value);
        }

        /// <summary>
        /// The event that occurs when a module is unloaded from the current process.
        /// </summary>
        public event EventHandler<EngineModuleUnloadEventArgs> ModuleUnload
        {
            add => events.AddHandler(nameof(ModuleUnload), value);
            remove => events.RemoveHandler(nameof(ModuleUnload), value);
        }

        /// <summary>
        /// The event that occurs when a thread is created in the current process.
        /// </summary>
        public event EventHandler<EngineThreadCreateEventArgs> ThreadCreate
        {
            add => events.AddHandler(nameof(ThreadCreate), value);
            remove => events.RemoveHandler(nameof(ThreadCreate), value);
        }

        /// <summary>
        /// The event that occurs when a thread exits in the current process.
        /// </summary>
        public event EventHandler<EngineThreadExitEventArgs> ThreadExit
        {
            add => events.AddHandler(nameof(ThreadExit), value);
            remove => events.RemoveHandler(nameof(ThreadExit), value);
        }

        public event EventHandler<EngineBreakpointHitEventArgs> BreakpointHit
        {
            add => events.AddHandler(nameof(BreakpointHit), value);
            remove => events.RemoveHandler(nameof(BreakpointHit), value);
        }

        private void AddEvent(string key, Delegate value)
        {
            events.AddHandler(key, value);

            lock (engines)
            {
                foreach (var engine in engines)
                    ((IDbgEngineInternal) engine).EventHandlers.AddHandler(engine, value);
            }
        }

        private void RemoveEvent(string key, Delegate value)
        {
            events.RemoveHandler(key, value);

            lock (engines)
            {
                foreach (var engine in engines)
                    ((IDbgEngineInternal) engine).EventHandlers.RemoveHandler(engine, value);
            }
        }

        #endregion

        /// <summary>
        /// Creates a new <typeparamref name="T"/> against a newly created process.
        /// </summary>
        /// <param name="options">The options to use to create the process.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to shutdown the engine thread.</param>
        /// <param name="initCallback">A callback that is used to initialize the newly created engine prior to launching the target process.</param>
        /// <returns>An <see cref="ICordbEngine"/> for debugging the specified process.</returns>
        public TEngine CreateProcess(CreateProcessOptions options, CancellationToken cancellationToken = default, Action<TEngine> initCallback = null)
        {
            CheckIfDisposed();

            var engine = CreateEngine();

            initCallback?.Invoke(engine);

            ((IDbgEngineInternal) engine).CreateProcess(options, cancellationToken);

            return engine;
        }

        public TEngine Attach(AttachProcessOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();

            var engine = CreateEngine();

            ((IDbgEngineInternal) engine).Attach(options, cancellationToken);

            return engine;
        }

        private TEngine CreateEngine()
        {
            lock (objLock)
            {
                var engine = NewEngine();

                engines.Add(engine);

                return engine;
            }
        }

        protected abstract TEngine NewEngine();

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
