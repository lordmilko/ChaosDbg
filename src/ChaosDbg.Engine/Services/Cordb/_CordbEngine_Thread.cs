using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ClrDebug;
using Win32Process = System.Diagnostics.Process;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void ThreadProc(object launchInfo)
        {
            CreateDebugTarget(launchInfo);

            Session.TargetCreated.Set();

            while (!Session.IsEngineCancellationRequested) //temp
            {
                Session.EngineThread.Dispatcher.DrainQueue();

                Thread.Sleep(100); //temp
            }
        }

        private void CreateDebugTarget(object launchInfo)
        {
            if (launchInfo is CreateProcessOptions c)
            {
                //Is the target executable a .NET Framework or .NET Core process?

                var kind = c.ExeKind ?? exeTypeDetector.Detect(c.CommandLine);

                NetInitCommon.Create(c, kind, InitCallback);
            }
            else
            {
                //Attach to an existing process
                var a = (AttachProcessOptions) launchInfo;

                var process = Win32Process.GetProcessById(a.ProcessId);

                Session.IsAttaching = true;

                if (process.Modules.Cast<ProcessModule>().Any(m => m.ModuleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase)))
                    NetFrameworkProcess.Attach(a, InitCallback);
                else
                    NetCoreProcess.Attach(a, InitCallback);
            }
        }

        private void InitCallback(
            CorDebug corDebug,
            CorDebugProcess process,
            CordbManagedCallback cb,
            CordbUnmanagedCallback ucb,
            bool is32Bit,
            string commandLine,
            bool isInterop)
        {
            RegisterCallbacks(cb);
            RegisterUnmanagedCallbacks(ucb);

            Session.CorDebug = corDebug;
            Session.ManagedCallback = cb;
            Session.UnmanagedCallback = ucb;
            Session.Process = new CordbProcess(process, Session, services, is32Bit, commandLine);
            Session.IsInterop = isInterop;
        }
    }
}
