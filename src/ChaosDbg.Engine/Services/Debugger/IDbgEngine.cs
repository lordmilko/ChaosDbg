using System;

namespace ChaosDbg
{
    public interface IDbgEngine : IDisposable
    {
        #region ChaosDbg Event Handlers

        /// <summary>
        /// The event that occurs when the engine wishes to print output to the console.
        /// </summary>
        event EventHandler<EngineOutputEventArgs> EngineOutput;

        /// <summary>
        /// The event that occurs when the debugger status changes (e.g. from broken to running).
        /// </summary>
        event EventHandler<EngineStatusChangedEventArgs> EngineStatusChanged;

        /// <summary>
        /// The event that occurs when a module is loaded into the current process.
        /// </summary>
        event EventHandler<EngineModuleLoadEventArgs> ModuleLoad;

        /// <summary>
        /// The event that occurs when a module is unloaded from the current process.
        /// </summary>
        event EventHandler<EngineModuleUnloadEventArgs> ModuleUnload;

        /// <summary>
        /// The event that occurs when a thread is created in the current process.
        /// </summary>
        event EventHandler<EngineThreadCreateEventArgs> ThreadCreate;

        /// <summary>
        /// The event that occurs when a thread exits in the current process.
        /// </summary>
        event EventHandler<EngineThreadExitEventArgs> ThreadExit;

        #endregion
    }
}
