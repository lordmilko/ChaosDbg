using ChaosLib;

namespace ChaosDbg.DbgEng
{
    public class DbgEngProcess : IDbgProcess
    {
        #region Overview

        /// <inheritdoc />
        public int Id { get; }

        /// <inheritdoc />
        public bool Is32Bit { get; }

        /// <inheritdoc />
        public string[] CommandLine { get; }

        #endregion
        #region Stores / Related Entities

        /// <inheritdoc cref="IDbgProcess.Threads" />
        public DbgEngThreadStore Threads { get; }

        /// <inheritdoc cref="IDbgProcess.Modules" />
        public DbgEngModuleStore Modules { get; }

        #endregion
        #region Debugger State

        public DbgEngSessionInfo Session { get; }

        //We don't store a status here as DbgEng only reports the status of the debugger, not necessarily the debuggee. e.g.
        //we get a ChangeEngineState notification to say that we're continung before we've even received our CreateProcess event.

        #endregion

        public DbgEngProcess(
            DbgEngSessionInfo session,
            DbgEngEngineServices services,
            int processId,
            bool is32Bit,
            string commandLine)
        {
            Id = processId;
            Is32Bit = is32Bit;

            if (commandLine != null)
                CommandLine = Shell32.CommandLineToArgvW(commandLine);

            Session = session;

            Threads = new DbgEngThreadStore(this);
            Modules = new DbgEngModuleStore(this, session, services);
        }

        #region IDbgProcess

        private ExternalDbgThreadStore externalThreadStore;

        /// <inheritdoc />
        IDbgThreadStore IDbgProcess.Threads => externalThreadStore ??= new ExternalDbgThreadStore(Threads);

        private ExternalDbgModuleStore externalModuleStore;

        /// <inheritdoc />
        IDbgModuleStore IDbgProcess.Modules => externalModuleStore ??= new ExternalDbgModuleStore(Modules);

        #endregion
    }
}
