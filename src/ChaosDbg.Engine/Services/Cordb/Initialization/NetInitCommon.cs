using System;
using System.Runtime.InteropServices;
using ChaosDbg.Metadata;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    abstract class NetInitCommon
    {
        public delegate void InitCallback(CordbManagedCallback cb, CordbUnmanagedCallback ucb, CorDebug corDebug, CordbTargetInfo target);

        public static void Create(
            CreateProcessOptions createProcessOptions,
            ExeKind kind,
            InitCallback initCallback)
        {
            /* Launch the process suspended, store all required state, and resume the process.
             * .NET Core requires we launch the process first and then extract an ICorDebug from it,
             * while .NET Framework creates an ICorDebug first and then launches the process directly
             * inside of it. All required initialization must be done by the time this method returns,
             * as our managed callbacks are going to immediately start running
             */

            switch (kind)
            {
                case ExeKind.Native: //The user specifically requested .NET debugging; we will assume it's a self extracting single file executable
                case ExeKind.NetCore:
                    NetCoreProcess.Create(createProcessOptions, initCallback);
                    break;

                case ExeKind.NetFramework:
                    NetFrameworkProcess.Create(createProcessOptions, initCallback);
                    break;

                default:
                    throw new UnknownEnumValueException(kind);
            }
        }

        protected static void GetCreateProcessArgs(
            CreateProcessOptions createProcessOptions,
            out CreateProcessFlags creationFlags,
            out STARTUPINFOW si)
        {
            si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>()
            };

            if (createProcessOptions.StartMinimized)
            {
                //Specifies that CreateProcess should look at the settings specified in wShowWindow
                si.dwFlags = STARTF.STARTF_USESHOWWINDOW;

                //We use ShowMinNoActive here instead of ShowMinimized, as ShowMinimized has the effect of causing our debugger
                //window to flash, losing and then regaining focus. If we never active the newly created process, we never lose
                //focus to begin with
                si.wShowWindow = ShowWindow.ShowMinNoActive;
            }

            creationFlags =
                CreateProcessFlags.CREATE_NEW_CONSOLE | //In the event ChaosDbg is invoked via some sort of command line tool, we want our debuggee to be created in a new window
                CreateProcessFlags.CREATE_SUSPENDED;    //Don't let the process start running; after we create it we want our debugger to attach to it
        }

        protected static void WithDbgShim(Action<DbgShim> action)
        {
            //Locate dbgshim, which should be in our output directory
            var dbgShimPath = DbgShimResolver.Resolve();

            //Load dbgshim, do something with it, then unload

            var hDbgShim = Kernel32.LoadLibrary(dbgShimPath);

            try
            {
                var dbgShim = new DbgShim(hDbgShim);

                action(dbgShim);
            }
            finally
            {
                Kernel32.FreeLibrary(hDbgShim);
            }
        }
    }
}
