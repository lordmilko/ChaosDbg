using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ChaosDbg.Metadata;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    partial class CordbEngine
    {
        private void ThreadProc(object launchInfo)
        {
            CreateDebugTarget(launchInfo);

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

                var kind = exeTypeDetector.Detect(c.CommandLine);

                NetInitCommon.Create(c, kind, InitCallback);
            }
            else
            {
                //Attach to an existing process
                var a = (AttachProcessOptions) launchInfo;

                var process = Process.GetProcessById(a.ProcessId);

                if (process.Modules.Cast<ProcessModule>()
                    .Any(m => m.ModuleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase)))
                    NetFrameworkProcess.Attach(a, InitCallback);
                else
                    NetCoreProcess.Attach(a, InitCallback);
            }
        }

        private void InitCallback(CordbManagedCallback cb, CordbUnmanagedCallback ucb, CorDebug corDebug, CordbTargetInfo target)
        {
            RegisterCallbacks(cb);
            RegisterUnmanagedCallbacks(ucb);

            Session.CorDebug = corDebug;
            Session.ManagedCallback = cb;
            Session.UnmanagedCallback = ucb;
            Session.Process = target.Process;

            Target = target;
        }
    }
}
