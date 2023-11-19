using System;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    //Engine thread/main debugger loop

    partial class DbgEngEngine
    {
        /// <summary>
        /// Represents the entry point of the DbgEng Engine Thread. The thread containing this entry point is created inside of <see cref="DbgEngSessionInfo"/>.
        /// </summary>
        /// <param name="launchInfo">Information about the debug target that should be launched.</param>
        private void ThreadProc(DbgLaunchInfo launchInfo)
        {
            //Clients can only be used on the thread that created them. Our UI Client is responsible for retrieving command inputs.
            //The real client however exists on the engine thread here
            Session.CreateEngineClient();

            //Sometimes we need to execute commands that only emit output to the output callbacks (e.g. OutputDisassemblyLines). Rather than pollute our normal output display,
            //we define a special purpose "buffer client" that has its own output callbacks that write to an array.
            Session.CreateBufferClient();

            /* The DbgEngEngine class will serve as both the output and event handlers. The reason for this is that only the class that owns an event
             * handler is allowed to invoke it. If we had separate classes for our engine, thread and event callbacks, we would have to write a lot of "middleware"
             * events to relay the same event over and over so the owning class can dispatch it further. Having everything in one big class simplifies dispatching
             * of events as well as accessing engine resources */
            EngineClient.OutputCallbacks = this;
            EngineClient.EventCallbacks = this;

            //We also serve as our input handler. This is the same thing that WinDbg does. Inside our input handler we call ReturnInput()
            //against the UI Client
            EngineClient.InputCallbacks = this;

            EngineClient.Control.EngineOptions =
                DEBUG_ENGOPT.INITIAL_BREAK | //Break immediately upon starting the debug session
                DEBUG_ENGOPT.FINAL_BREAK;    //Break when the debug target terminates

            //https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/symbol-options
            EngineClient.Symbols.SymbolOptions =
                //Standard default settings
                SYMOPT.CASE_INSENSITIVE     | //Ignore case when searching for symbols.
                SYMOPT.UNDNAME              | //Undecorate symbols when they are displayed/ignore decorations in symbol searches
                SYMOPT.DEFERRED_LOADS       | //Don't load symbols for modules until the debugger actually requires them
                SYMOPT.OMAP_FIND_NEAREST    | //Standard default setting. Use the nearest symbol when optimization causes an expected symbol to not exist at a given location
                SYMOPT.LOAD_LINES           | //Read line information from source files for use in source level debugging
                SYMOPT.FAIL_CRITICAL_ERRORS | //Don't display a dialog box if an error occurs during symbol loading. Not sure why one would though
                SYMOPT.AUTO_PUBLICS         | //Fallback to using public symbols in the PDB as a last resort
                SYMOPT.NO_IMAGE_SEARCH      |  //Don't search the disk for a copy of the image when symbols are loaded

                //Custom ChaosDbg default settings
                0; //None

            //Launch the target process and get some basic information about it
            Target = CreateDebugTarget(launchInfo);

            //Now let's attach to it
            EngineClient.AttachProcess(0, Target.ProcessId, DEBUG_ATTACH.DEFAULT);

            //Enter the main engine loop. This method will not return until the debugger ends
            EngineLoop();
        }

        /// <summary>
        /// The main engine loop of the engine thread. This method does not return utnil the debugger is terminated.
        /// </summary>
        private void EngineLoop()
        {
            while (!IsEngineCancellationRequested)
            {
                //Go to sleep and wait for an event to occur. We can force the debugger to wake up
                //via our UiClient by calling DebugControl.SetInterrupt()
                EngineClient.Control.WaitForEvent(DEBUG_WAIT.DEFAULT, Kernel32.INFINITE);

                //When disposing our debug session on the UI thread, we will cancel our CTS and then attempt to call DebugControl.SetInterrupt().
                //If we can see cancellation was requested, the engine is shutting down, and we should break out of the engine loop
                if (IsEngineCancellationRequested)
                    break;

                //Notify all DebugClient instances about the current status of the debuggee (e.g. the current instruction we've stopped at)
                EngineClient.Control.OutputCurrentState(DEBUG_OUTCTL.ALL_CLIENTS, DEBUG_CURRENT.DEFAULT);

                //Repeatedly request input from the user until they resume the debuggee.
                //If the engine was terminated while inside of the input loop, we'll see
                //that cancellation was requested at the start of the next EngineLoop iteration
                InputLoop();
            }
        }

        /// <summary>
        /// The input loop of the engine thread. This method runs whenever the debuggee is broken into, and does
        /// not return into the debuggee is resumed.
        /// </summary>
        private void InputLoop()
        {
            while (!IsEngineCancellationRequested && Target.Status == DEBUG_STATUS.BREAK)
            {
                //Go to sleep and wait for a command to be enqueued on the UI thread. We will wake up
                //when the UiClient calls DebugClient.ExitDispatch()
                EngineClient.DispatchCallbacks(Kernel32.INFINITE);

                //Similar to breakint out of the EngineLoop, when we want to end the debugger session we'll cancel our CTS and then attempt to call
                //DebugClient.ExitDispatch(). 
                if (IsEngineCancellationRequested)
                    break;

                //Process any commands that were dispatched to the engine thread
                Commands.DrainQueue();
            }
        }

        /// <summary>
        /// Launches the specified target (e.g. creates the target process) and creates a <see cref="DbgEngTargetInfo"/> that provides key information about the target.
        /// </summary>
        private DbgEngTargetInfo CreateDebugTarget(DbgLaunchInfo launchInfo)
        {
            var si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>()
            };

            if (launchInfo.StartMinimized)
            {
                //Specifies that CreateProcess should look at the settings specified in wShowWindow
                si.dwFlags = STARTF.STARTF_USESHOWWINDOW;

                //We use ShowMinNoActive here instead of ShowMinimized, as ShowMinimized has the effect of causing our debugger
                //window to flash, losing and then regaining focus. If we never active the newly created process, we never lose
                //focus to begin with
                si.wShowWindow = ShowWindow.ShowMinNoActive;
            }

            var creationFlags =
                CreateProcessFlags.CREATE_NEW_CONSOLE | //In the event ChaosDbg is invoked via some sort of command line tool, we want our debuggee to be created in a new window
                CreateProcessFlags.CREATE_SUSPENDED;    //Don't let the process start running; after we create it we want our debugger to attach to it

            PROCESS_INFORMATION pi;

            Kernel32.CreateProcessW(
                launchInfo.ProcessName,
                creationFlags,
                IntPtr.Zero,
                Environment.CurrentDirectory,
                ref si,
                out pi
            );

            var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

            var target = new DbgEngTargetInfo(launchInfo.ProcessName, pi.dwProcessId, is32Bit);

            //We have everything we need, now finally close the process and thread handles we were given.
            //We don't need to resume the thread before closing it, DbgEng can get its own handle when its ready to resume
            Kernel32.CloseHandle(pi.hProcess);
            Kernel32.CloseHandle(pi.hThread);

            return target;
        }
    }
}
