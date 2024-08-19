using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using ClrDebug.DbgEng;
using static ClrDebug.DbgEng.DEBUG_CLASS_QUALIFIER;

namespace ChaosDbg.DbgEng
{
    //Engine startup/state/general code

    public partial class DbgEngEngine : IDbgEngineInternal
    {
        public DbgEngProcess ActiveProcess => Session.ActiveProcess;

        /// <summary>
        /// Gets the current <see cref="EngineClient"/>. This property should only be accessed on the engine thread.
        /// </summary>
        public DebugClient EngineClient => Session.EngineClient;

        /// <summary>
        /// Gets whether the engine has been cancelled and is in the process of shutting down.
        /// </summary>
        public bool IsEngineCancellationRequested => Session.IsEngineCancellationRequested;

        public DebugClient ActiveClient => Session.ActiveClient;

        #region Services

        private DbgEngTtdServices ttd;

        /// <summary>
        /// Gets services used for interacting with Time Travel Debugging traces.<para/>
        /// If the current debug target is not <see cref="DEBUG_CLASS_QUALIFIER.USER_WINDOWS_IDNA"/>,
        /// this property will throw.
        /// </summary>
        public DbgEngTtdServices TTD
        {
            get
            {
                if (ttd == null)
                {
                    var debuggeeType = ActiveClient.Control.DebuggeeType;

                    if (debuggeeType.Class == DEBUG_CLASS.USER_WINDOWS && debuggeeType.Qualifier == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_IDNA)
                        ttd = new DbgEngTtdServices(this);
                    else
                    {
                        string qualifierStr;

                        //Several enums share the same numeric value, so get the right string based on the class.
                        //ToString() will not work, you have to use nameof instead
                        switch (debuggeeType.Class)
                        {
                            case DEBUG_CLASS.KERNEL:
                                qualifierStr = debuggeeType.Qualifier switch
                                {
                                    KERNEL_CONNECTION => nameof(KERNEL_CONNECTION),
                                    KERNEL_LOCAL => nameof(KERNEL_LOCAL),
                                    KERNEL_SMALL_DUMP => nameof(KERNEL_SMALL_DUMP),
                                    KERNEL_FULL_DUMP => nameof(KERNEL_FULL_DUMP),
                                    _ => debuggeeType.Qualifier.ToString()
                                };
                                break;

                            case DEBUG_CLASS.USER_WINDOWS:
                                qualifierStr = debuggeeType.Qualifier switch
                                {
                                    USER_WINDOWS_PROCESS => nameof(USER_WINDOWS_PROCESS),
                                    USER_WINDOWS_PROCESS_SERVER => nameof(USER_WINDOWS_PROCESS_SERVER),
                                    USER_WINDOWS_SMALL_DUMP => nameof(USER_WINDOWS_SMALL_DUMP),
                                    USER_WINDOWS_DUMP => nameof(USER_WINDOWS_DUMP),
                                    _ => debuggeeType.Qualifier.ToString()
                                };
                                break;

                            default:
                                qualifierStr = debuggeeType.Qualifier.ToString();
                                break;
                        }

                        throw new InvalidOperationException($"TTD services cannot be used with a debuggee of type '{qualifierStr}'");
                    }
                }

                return ttd;
            }
        }

        #endregion
        #region State

        /// <summary>
        /// Gets the container containing the entities used to manage the current <see cref="DbgEngEngine"/> session.
        /// </summary>
        public DbgEngSessionInfo Session { get; private set; }

        #endregion

        private readonly DbgEngEngineServices services;
        private readonly DebugEngineProvider engineProvider;
        private bool disposed;

        public DbgEngEngine(DbgEngEngineServices services, DebugEngineProvider engineProvider)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (engineProvider == null)
                throw new ArgumentNullException(nameof(engineProvider));

            this.services = services;
            this.engineProvider = engineProvider;
        }

        /// <summary>
        /// Notifies the <see cref="EngineClient"/> that a command is available for processing.
        /// </summary>
        internal void WakeEngineForInput()
        {
            if (Session.EngineThreadId == Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Engine should not be awoken from the engine thread");

            Session.ActiveClient.ExitDispatch(Session.EngineClientRaw);
        }

        [Obsolete("Do not call this method. Use DebugEngineProvider.CreateProcess() instead")]
        void IDbgEngineInternal.CreateProcess(CreateProcessTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use DebugEngineProvider.Attach() instead")]
        void IDbgEngineInternal.Attach(AttachProcessTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        [Obsolete("Do not call this method. Use DebugEngineProvider.OpenDump() instead")]
        void IDbgEngineInternal.OpenDump(OpenDumpTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        internal void ConnectServer(ServerTargetOptions options, CancellationToken cancellationToken) =>
            CreateSession(options, cancellationToken);

        private void CreateSession(LaunchTargetOptions options, CancellationToken cancellationToken)
        {
            if (Session != null)
                throw new InvalidOperationException($"Cannot launch target {options}: an existing session is already running.");

            Session = new DbgEngSessionInfo(
                services,
                () => ThreadProc(options),
#pragma warning disable CS0618
                services.SafeDebugCreate(options.UseDbgEngSymOpts ?? true), //g_Machine is protected by the DebugEngineProvider
#pragma warning restore CS0618
                options.DbgEngEngineId,
                cancellationToken
            );

            //We must start the debugger thread AFTER the Session variable has been assigned to
            Session.Start();

            try
            {
                //We want to be able to wait on the TCS with our CancellationToken, but this will result in an AggregateException being thrown on failure.
                //GetAwaiter().GetResult() won't let you use your CancellationToken in conjunction with them
                Session.TargetCreated.Wait(cancellationToken);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        #region IDbgEngine

        IDbgProcess IDbgEngine.ActiveProcess => ActiveProcess;

        IDbgSessionInfo IDbgEngine.Session => Session;

        #endregion

        public void Dispose()
        {
            if (disposed)
                return;

            engineProvider.Remove(this);

            Session?.Dispose();
            Session = null;
        }
    }
}
