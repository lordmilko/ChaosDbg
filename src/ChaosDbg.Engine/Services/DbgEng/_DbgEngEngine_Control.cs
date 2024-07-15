using System;
using System.Collections.Generic;
using System.Threading;
using ChaosDbg.DbgEng.Model;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Debugger interaction (start, stop, break, step, etc)

    partial class DbgEngEngine
    {
        /// <summary>
        /// Executes a command in the context of the engine thread.
        /// </summary>
        /// <typeparam name="T">The type of value that is returned from the command.</typeparam>
        /// <param name="func">The command to execute.</param>
        /// <returns>The result of the executed command.</returns>
        public T Invoke<T>(Func<DebugClient, T> func) =>
            Session.EngineThread.Invoke(() => func(Session.EngineClient));

        public void Invoke(Action<DebugClient> action) =>
            Session.EngineThread.Invoke(() => action(Session.EngineClient));

        public void Execute(string command) =>
            Invoke(c => c.Control.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT));

        public string[] ExecuteBufferedCommand(string command) =>
            ExecuteBufferedCommand(c => c.Control.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT));

        /// <summary>
        /// Executes a command that emits string values to output callbacks that should be captured and returned
        /// without affecting the output of the primary output callbacks.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        /// <returns>The text that was emitted by the command to the output callbacks.</returns>
        public string[] ExecuteBufferedCommand(Action<DebugClient> action) =>
            Session.EngineThread.Invoke(() => Session.ExecuteBufferedCommand(action));

        public void WaitForBreak(CancellationToken cancellationToken = default)
        {
            //We do not receive engine status events until we've waited for an initial debugger event,
            //which means we'll race between CreateProcess() returning (which occurs as soon as the target
            //has actually been created) and an actual WaitForEvent occurring

            if (Session.Status != EngineStatus.Break)
                Session.BreakEvent.Wait(cancellationToken);
        }

        public void Continue() => Invoke(c => c.Control.ExecutionStatus = DEBUG_STATUS.GO);

        #region Model

        /// <summary>
        /// Gets a dynamic object that encapsulates the root namespace of the DbgEng Data Model.
        /// </summary>
        /// <returns>A dynamic object that encapsulates the root namespace of the DbgEng Data Model.</returns>
        public dynamic GetModelRootNamespace()
        {
            var dataModel = ActiveClient.HostDataModelAccess.DataModel;
            var dataModelManager = dataModel.manager;

            var ns = dataModelManager.RootNamespace;

            return DynamicDbgModelObject.New(ns, null, dataModelManager);
        }

        /// <summary>
        /// Gets a dynamic object that should represent the @$cursession of the DbgEng Data Model.
        /// </summary>
        /// <returns>A dynamic object that should represent the @$cursession of the DbgEng Data Model.</returns>
        public dynamic GetModelSession()
        {
            //I don't know 100% if this is what @$cursession does, but this technique of indexing
            //into the list of sessions using the IDebugHostContext was derived from reversing how the JavaScript Provider
            //provides its session value to JavaScript code

            var dataModel = ActiveClient.HostDataModelAccess.DataModel;
            var dataModelManager = dataModel.manager;
            var debugHost = dataModel.host;

            var (session, metadata) = GetRawModelSession(dataModelManager, debugHost);

            return DynamicDbgModelObject.New(session, metadata, dataModelManager);
        }

        /// <summary>
        /// Gets a dynamic object that should represent the @$curprocess of the DbgEng Data Model.
        /// </summary>
        /// <returns>A dynamic object that should represent the @$curprocess of the DbgEng Data Model.</returns>
        public dynamic GetModelProcess()
        {
            //Same as above: I'm guessing this is how @$curprocess works

            var dataModel = ActiveClient.HostDataModelAccess.DataModel;
            var dataModelManager = dataModel.manager;
            var debugHost = dataModel.host;

            var (session, _) = GetRawModelSession(dataModelManager, debugHost);

            //Conceptually, the Processes object has an indexer like "this[IDebugHostContext value]"

            var processes = session.GetKeyValue("Processes").@object;

            IndexableConcept indexableProcesses = processes.Concept.Indexable.conceptInterface;

            var indexValue = dataModelManager.CreateIntrinsicObject(ModelObjectKind.ObjectContext, debugHost.CurrentContext.Raw);
            var process = indexableProcesses.GetAt(processes.Raw, 1, new[] { indexValue.Raw });

            return DynamicDbgModelObject.New(process.@object, process.metadata, dataModelManager);
        }

        private (ModelObject session, KeyStore metadata) GetRawModelSession(DataModelManager dataModelManager, DebugHost debugHost)
        {
            var sessions = dataModelManager.RootNamespace
                .GetKeyValue("Debugger").@object
                .GetKeyValue("Sessions").@object;

            //This gives us the @$debuggerRootNamespace.Debugger.Sessions part
            IndexableConcept indexableSessions = sessions.Concept.Indexable.conceptInterface;

            //Conceptually, the Sessions object has an indexer like "this[IDebugHostContext value]"

            //Create the argument to pass into the indexer
            var indexValue = dataModelManager.CreateIntrinsicObject(ModelObjectKind.ObjectContext, debugHost.CurrentContext.Raw);

            var session = indexableSessions.GetAt(sessions.Raw, 1, new[] { indexValue.Raw });

            return (session.@object, session.metadata);
        }

        #endregion

        public GetNextSymbolMatchResult[] GetSymbols(string[] patterns)
        {
            var debugSymbols = ActiveClient.Symbols;

            var results = new List<GetNextSymbolMatchResult>();

            foreach (var name in patterns)
            {
                var handle = debugSymbols.StartSymbolMatch(name);

                try
                {
                    while (debugSymbols.TryGetNextSymbolMatch(handle, out var result) == HRESULT.S_OK)
                    {
                        results.Add(result);
                    }
                }
                finally
                {
                    debugSymbols.EndSymbolMatch(handle);
                }
            }

            return results.ToArray();
        }

        public DbgEngFrame[] GetStackTrace()
        {
            return Invoke(engine =>
            {
                //g_DefaultStackTraceDepth is 256 (0x100) in modern versions of DbgEng
                var frames = engine.Control.GetStackTrace(0, 0, 0, 256);

                var results = new List<DbgEngFrame>();

                foreach (var frame in frames)
                {
                    string name = null;

                    if (engine.Symbols.TryGetNameByOffset(frame.InstructionOffset, out var symbol) == HRESULT.S_OK)
                    {
                        name = symbol.NameBuffer;

                        if (symbol.Displacement > 0)
                            name = $"{name}+{symbol.Displacement:X}";
                    }

                    results.Add(new DbgEngFrame(name, frame));
                }

                return results.ToArray();
            });
        }
    }
}
