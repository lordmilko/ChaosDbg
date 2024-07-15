using System;
using System.Collections;
using System.Collections.Generic;
using ChaosLib;
using static ClrDebug.HRESULT;

namespace ChaosDbg.DbgEng
{
    public class DbgEngProcessStore : IDbgProcessStoreInternal, IEnumerable<DbgEngProcess>
    {
        private object processLock = new object();

        private Dictionary<int, DbgEngProcess> processes = new Dictionary<int, DbgEngProcess>();

        public DbgEngProcess ActiveProcess { get; set; }

        private DbgEngSessionInfo session;
        private DbgEngEngineServices services;

        public DbgEngProcessStore(DbgEngSessionInfo session, DbgEngEngineServices services)
        {
            this.session = session;
            this.services = services;
        }

        public void Add(DbgEngProcess process)
        {
            lock (processLock)
            {
                processes.Add(process.Id, process);

                if (ActiveProcess == null)
                    ActiveProcess = process;
            }
        }

        public void Remove(DbgEngProcess process)
        {
            throw new NotImplementedException();
        }

        public void RefreshActiveProcess()
        {
            //g_EventProcess does not get set until we actually start receiving events for the process. There are a number of ChangeEngineState events that will occur
            //before the process is actually initialized. The debugger will report to us that "it is running", which is kind of a separate concept to whether the _process_
            //is running.

            //todo: not sure if this is what occurs each time theres an event when you're debugging multiple processes?
            if (session.ActiveClient.SystemObjects.TryGetCurrentProcessSystemId(out var pid) == S_OK)
            {
                lock (processLock)
                {
                    //If we don't have the PID yet, we haven't received the CreateProcess event yet, so just ignore
                    if (!processes.TryGetValue(pid, out var process))
                        return;

                    ActiveProcess = process;
                }
            }
        }

        public DbgEngProcess GetOrCreateProcess(long hProcess)
        {
            lock (processLock)
            {
                var processId = session.EngineClient.SystemObjects.CurrentProcessSystemId;

                if (processes.TryGetValue(processId, out var process))
                    return process;

                var peb = new RemotePeb((IntPtr) hProcess);
                var commandLine = peb.ProcessParameters.CommandLine;
                process = new DbgEngProcess(session, services, processId, Kernel32.IsWow64ProcessOrDefault((IntPtr) hProcess), commandLine);

                processes[processId] = process;

                return process;
            }
        }

        public DbgEngProcess GetProcessFromEngineId(int processEngineId)
        {
            throw new NotImplementedException();
        }

        #region IDbgProcessStore

        IDbgProcess IDbgProcessStoreInternal.ActiveProcess => ActiveProcess;

        #endregion

        public IEnumerator<DbgEngProcess> GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        IEnumerator<IDbgProcess> IDbgProcessStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
