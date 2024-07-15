using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosLib;

namespace ChaosDbg
{
    /// <summary>
    /// Provides facilities for creating and accessing <see cref="IDbgEngine"/> instances.<para/>
    /// This type is the entry point for launching a debug target.
    /// </summary>
    public partial class DebugEngineProvider
    {
        private bool disposed;

        private List<IDbgEngine> engines = new List<IDbgEngine>();
        private object enginesLock = new object();

        //DbgEng is not thread safe. Attempting to create multiple machines concurrently will overwrite g_Machine which can cause other DbgEng instances
        //to crash when the g_Machine gets nulled out
        private static object dbgEngInstanceLock = new object();

        private IDbgEngine activeEngine;

        public IDbgEngine ActiveEngine
        {
            get
            {
                if (activeEngine != null)
                    return activeEngine;

                lock (enginesLock)
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

        //Engine Providers that derive from DebugEngineProvider should store a copy of the items in this list inside themselves inside their NewEngine() override
        protected EventHandlerList events = new EventHandlerList();

        public void ClearEventHandlers()
        {
            events = new EventHandlerList();
        }

        /// <summary>
        /// The event that occurs when the engine has initialized its <see cref="IDbgSessionInfo"/> but has not yet launched a debug target.
        /// </summary>
        public event EventHandler<EngineInitializedEventArgs> EngineInitialized
        {
            add => AddEvent(nameof(EngineInitialized), value);
            remove => RemoveEvent(nameof(EngineInitialized), value);
        }

        /// <summary>
        /// The event that occurs when the engine wishes to print output to the console.
        /// </summary>
        public event EventHandler<EngineOutputEventArgs> EngineOutput
        {
            add => AddEvent(nameof(EngineOutput), value);
            remove => RemoveEvent(nameof(EngineOutput), value);
        }

        /// <summary>
        /// The event that occurs when the debugger status changes (e.g. from broken to running).
        /// </summary>
        public event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged
        {
            add => AddEvent(nameof(EngineStatusChanged), value);
            remove => RemoveEvent(nameof(EngineStatusChanged), value);
        }

        /// <summary>
        /// The event that occurs when a fatal exception occurs inside the debugger engine.
        /// </summary>
        public event EventHandler<EngineFailureEventArgs> EngineFailure
        {
            add => AddEvent(nameof(EngineFailure), value);
            remove => RemoveEvent(nameof(EngineFailure), value);
        }

        /// <summary>
        /// The event that occurs when a module is loaded into the current process.
        /// </summary>
        public event EventHandler<EngineModuleLoadEventArgs> ModuleLoad
        {
            add => AddEvent(nameof(ModuleLoad), value);
            remove => RemoveEvent(nameof(ModuleLoad), value);
        }

        /// <summary>
        /// The event that occurs when a module is unloaded from the current process.
        /// </summary>
        public event EventHandler<EngineModuleUnloadEventArgs> ModuleUnload
        {
            add => AddEvent(nameof(ModuleUnload), value);
            remove => RemoveEvent(nameof(ModuleUnload), value);
        }

        /// <summary>
        /// The event that occurs when a thread is created in the current process.
        /// </summary>
        public event EventHandler<EngineThreadCreateEventArgs> ThreadCreate
        {
            add => AddEvent(nameof(ThreadCreate), value);
            remove => RemoveEvent(nameof(ThreadCreate), value);
        }

        /// <summary>
        /// The event that occurs when a thread exits in the current process.
        /// </summary>
        public event EventHandler<EngineThreadExitEventArgs> ThreadExit
        {
            add => AddEvent(nameof(ThreadExit), value);
            remove => RemoveEvent(nameof(ThreadExit), value);
        }

        public event EventHandler<EngineBreakpointHitEventArgs> BreakpointHit
        {
            add => AddEvent(nameof(BreakpointHit), value);
            remove => RemoveEvent(nameof(BreakpointHit), value);
        }

        public event EventHandler<EngineExceptionHitEventArgs> ExceptionHit
        {
            add => AddEvent(nameof(ExceptionHit), value);
            remove => RemoveEvent(nameof(ExceptionHit), value);
        }

        private void AddEvent(string key, Delegate value)
        {
            events.AddHandler(key, value);

            lock (engines)
            {
                foreach (var engine in engines)
                    ((IDbgEngineInternal) engine).EventHandlers.AddHandler(key, value);
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

        private readonly DbgEngEngineServices dbgEngEngineServices;
        private readonly CordbEngineServices cordbEngineServices;

        /// <summary>
        /// Provides a simplified means of launching <see cref="CordbEngine"/> instances with commonly used configuration options.
        /// </summary>
        public CordbEngineLaunchExtensions Cordb { get; }

        /// <summary>
        /// Provides a simplified means of launching <see cref="DbgEngEngine"/> instances with commonly used configuration options.
        /// </summary>
        public DbgEngEngineLaunchExtensions DbgEng { get; }

        public DebugEngineProvider(DbgEngEngineServices dbgEngEngineServices, CordbEngineServices cordbEngineServices)
        {
            this.dbgEngEngineServices = dbgEngEngineServices;
            this.cordbEngineServices = cordbEngineServices;

            Cordb = new CordbEngineLaunchExtensions(this);
            DbgEng = new DbgEngEngineLaunchExtensions(this);
        }

        /// <summary>
        /// Creates a new <see cref="IDbgEngine"/> against a newly created process.
        /// </summary>
        /// <param name="engineKind">The type of engine to use to launch the target process.</param>
        /// <param name="options">The options to use to create the process.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to shutdown the engine thread.</param>
        /// <returns>An <see cref="IDbgEngine"/> for debugging the specified process.</returns>
        public IDbgEngine CreateProcess(DbgEngineKind engineKind, CreateProcessTargetOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();
            CheckIfSupported(engineKind, options.Kind);
            CheckRequiredEventHandlers(engineKind);

            var engine = CreateEngine(engineKind);

            ((IDbgEngineInternal) engine).CreateProcess(options, cancellationToken);

            return engine;
        }

        public IDbgEngine Attach(DbgEngineKind engineKind, AttachProcessTargetOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();
            CheckIfSupported(engineKind, options.Kind);
            CheckRequiredEventHandlers(engineKind);

            var engine = CreateEngine(engineKind);

            ((IDbgEngineInternal) engine).Attach(options, cancellationToken);

            return engine;
        }

        public IDbgEngine OpenDump(DbgEngineKind engineKind, OpenDumpTargetOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();
            CheckIfSupported(engineKind, options.Kind);
            CheckRequiredEventHandlers(engineKind);

            var engine = CreateEngine(engineKind);

            ((IDbgEngineInternal) engine).OpenDump(options, cancellationToken);

            return engine;
        }

        public IDbgEngine ConnectServer(DbgEngineKind engineKind, ServerTargetOptions options, CancellationToken cancellationToken = default)
        {
            CheckIfDisposed();
            CheckIfSupported(engineKind, options.Kind);
            CheckRequiredEventHandlers(engineKind);

            var engine = CreateEngine(engineKind);

            ((DbgEngEngine) engine).ConnectServer(options, cancellationToken);

            return engine;
        }

        protected virtual IDbgEngine CreateEngine(DbgEngineKind engineKind)
        {
            var engine = NewEngine(engineKind, false);

            lock (enginesLock)
            {
                engines.Add(engine);

                return engine;
            }
        }

        private IDbgEngine NewEngine(DbgEngineKind engineKind, bool throwIfUnavailable)
        {
            switch (engineKind)
            {
                case DbgEngineKind.Cordb:
                case DbgEngineKind.Interop:
                {
                    var engine = new CordbEngine(cordbEngineServices, this);
                    engine.EventHandlers.AddHandlers(events);
                    return engine;
                }
                    

                case DbgEngineKind.DbgEng:
                {
                    if (Monitor.TryEnter(dbgEngInstanceLock))
                    {
                        Log.Debug<DebugEngineProvider>("Acquired DbgEng instance lock");

                        var engine = new DbgEngEngine(dbgEngEngineServices, this);
                        engine.EventHandlers.AddHandlers(events);
                        return engine;
                    }

                    if (throwIfUnavailable)
                        throw new InvalidOperationException($"Cannot create {engineKind}: engine is a singleton that is already in use");

                    Log.Debug<DebugEngineProvider>("Waiting for DbgEng instance lock");

                    Monitor.Enter(dbgEngInstanceLock);
                    {
                        Log.Debug<DebugEngineProvider>("Acquired DbgEng instance lock after waiting");

                        var engine = new DbgEngEngine(dbgEngEngineServices, this);
                        engine.EventHandlers.AddHandlers(events);
                        return engine;
                    }
                }
                default:
                    throw new UnknownEnumValueException(engineKind);
            }
        }

        public virtual void Remove(IDbgEngine engine)
        {
            lock (enginesLock)
            {
                if (activeEngine != null && activeEngine.Equals(engine))
                    activeEngine = default;

                engines.Remove(engine);
            }

            if (engine is DbgEngEngine)
            {
                Log.Debug<DebugEngineProvider>("Releasing DbgEng instance lock");

                Monitor.Exit(dbgEngInstanceLock);
            }
        }

        internal void WithDbgEng(Action<DbgEngEngineServices> action)
        {
            lock (dbgEngInstanceLock)
                action(dbgEngEngineServices);
        }

        protected void CheckRequiredEventHandlers(DbgEngineKind engineKind)
        {
            if (events[nameof(EngineFailure)] == null)
                throw new InvalidOperationException($"Cannot create {engineKind} instance: an '{nameof(EngineFailure)}' event handler has not been set.");
        }

        public static bool CheckIfSupported(DbgEngineKind engineKind, LaunchTargetKind launchTargetKind)
        {
            switch (engineKind)
            {
                case DbgEngineKind.Cordb:
                case DbgEngineKind.Interop:
                    switch (launchTargetKind)
                    {
                        case LaunchTargetKind.CreateProcess:
                        case LaunchTargetKind.AttachProcess:
                            return true;

                        case LaunchTargetKind.Kernel:
                        case LaunchTargetKind.OpenDump:
                        case LaunchTargetKind.Server:
                            return false;

                        default:
                            throw new UnknownEnumValueException(launchTargetKind);
                    }

                case DbgEngineKind.DbgEng:
                    switch (launchTargetKind)
                    {
                        case LaunchTargetKind.CreateProcess:
                        case LaunchTargetKind.AttachProcess:
                        case LaunchTargetKind.Kernel:
                        case LaunchTargetKind.OpenDump:
                        case LaunchTargetKind.Server:
                            return true;

                        default:
                            throw new UnknownEnumValueException(launchTargetKind);
                    }

                default:
                    throw new UnknownEnumValueException(launchTargetKind);
            }
        }

        protected void CheckIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            lock (enginesLock)
            {
                foreach (var engine in engines)
                    engine.Dispose();
            }

            disposed = true;
        }
    }
}
