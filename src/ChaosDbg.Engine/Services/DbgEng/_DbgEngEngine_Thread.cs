using System;
using System.Diagnostics;
using ChaosDbg.DbgEng.Server;
using ChaosDbg.TTD;
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
        /// <param name="options">Information about the debug target that should be launched.</param>
        private void ThreadProc(LaunchTargetOptions options)
        {
            try
            {
                //Clients can only be used on the thread that created them. Our UI Client is responsible for retrieving command inputs.
                //The real client however exists on the engine thread here
                Session.CreateEngineClient(options);

                //Sometimes we need to execute commands that only emit output to the output callbacks (e.g. OutputDisassemblyLines). Rather than pollute our normal output display,
                //we define a special purpose "buffer client" that has its own output callbacks that write to an array.
                Session.CreateBufferClient();

                var engineOpts =
                    DEBUG_ENGOPT.FINAL_BREAK;    //Break when the debug target terminates

                if (options.InitialBreak)
                    engineOpts |= DEBUG_ENGOPT.INITIAL_BREAK; //Break immediately upon starting the debug session

                EngineClient.Control.EngineOptions = engineOpts;

                //If we aren't allowing DbgEng to control DbgHelp options, our IAT hook for DbgHelp on DbgEng will intercept this

                //https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/symbol-options
                EngineClient.Symbols.SymbolOptions =
                    //Standard default settings
                    SYMOPT.CASE_INSENSITIVE     | //Ignore case when searching for symbols.
                    SYMOPT.UNDNAME              | //Undecorate symbols when they are displayed/ignore decorations in symbol searches
                    SYMOPT.DEFERRED_LOADS       | //Don't load symbols for modules until the debugger actually requires them
                    SYMOPT.OMAP_FIND_NEAREST    | //Standard default setting. Use the nearest symbol when optimization causes an expected symbol to not exist at a given location
                    SYMOPT.NO_UNQUALIFIED_LOADS | //While the documentation doesn't say it, this is included by default. This ensures that typing a random command like "foo" doesn't cause the debugger to try and resolve it as a symbol
                    SYMOPT.LOAD_LINES           | //Read line information from source files for use in source level debugging
                    SYMOPT.FAIL_CRITICAL_ERRORS | //Don't display a dialog box if an error occurs during symbol loading. Not sure why one would though
                    SYMOPT.AUTO_PUBLICS         | //Fallback to using public symbols in the PDB as a last resort
                    SYMOPT.NO_IMAGE_SEARCH      | //Don't search the disk for a copy of the image when symbols are loaded

                    //Custom ChaosDbg default settings
                    0; //None

                //DbgEng will emit a "Path validation summary" twice if g_SymbolSearchPathExpanded is not set.
                //It will be set to the "real" symbol path by dbgeng!ProcessInfo::ProcessInfo. From my quick investigation,
                //not sure if it's possible to suppress the entire "Path validation summary" by setting the correct symbol path
                //to begin with
                EngineClient.Symbols.SymbolPath = string.Empty;

                //Hook up callbacks last. If multiple engines are running simultaneously, a broadcast to all clients may result in us receiving notifications that don't belong to us.
                //We don't want to trip over such alerts when we're not ready to receive them yet

                /* The DbgEngEngine class will serve as both the output and event handlers. The reason for this is that only the class that owns an event
                 * handler is allowed to invoke it. If we had separate classes for our engine, thread and event callbacks, we would have to write a lot of "middleware"
                 * events to relay the same event over and over so the owning class can dispatch it further. Having everything in one big class simplifies dispatching
                 * of events as well as accessing engine resources */
                EngineClient.OutputCallbacks = this;
                EngineClient.EventCallbacks = this;

                //We also serve as our input handler. This is the same thing that WinDbg does. Inside our input handler we call ReturnInput()
                //against the UI Client
                EngineClient.InputCallbacks = this;

                //We've about to launch our debug target. If the user wants to do any last minute customization of the debugger,
                //now's their chance
                RaiseEngineInitialized();

                HRESULT hr;

                switch (options.Kind)
                {
                    case LaunchTargetKind.CreateProcess:
                        hr = TryCreateProcess(options);
                        break;

                    case LaunchTargetKind.AttachProcess:
                        hr = TryAttachProcess(options);
                        break;

                    case LaunchTargetKind.OpenDump:
                        hr = TryOpenDump(options);
                        break;

                    case LaunchTargetKind.Server:
                        hr = TryConnectServer(options);
                        break;

                    default:
                        throw new UnknownEnumValueException(options.Kind);
                }

                switch (hr)
                {
                    case HRESULT.ERROR_NOT_SUPPORTED:
                    case HRESULT.STATUS_NOT_SUPPORTED:
                        throw new DebuggerInitializationException($"Failed to attach to process: process most likely does not match architecture of debugger ({(IntPtr.Size == 4 ? "x86" : "x64")}).", hr);

                    default:
                        hr.ThrowOnNotOK();
                        break;
                }

                Session.TargetCreated.SetResult();

                //Enter the main engine loop. This method will not return until the debugger ends
                EngineLoop();
            }
            catch (Exception ex)
            {
                //If we already set these, all good
                Session.TargetCreated.TrySetResult();
                Session.BreakEvent.TrySetResult();

                Log.Error<DbgEngEngine>(ex, "An unhandled exception occurred on the DbgEng Engine Thread: {message}", ex.Message);

                throw;
            }
        }

        /// <summary>
        /// The main engine loop of the engine thread. This method does not return utnil the debugger is terminated.
        /// </summary>
        private void EngineLoop()
        {
            try
            {
                /* Ostensibly, we want to do a WaitForEvent with 0 timeout prior to enterting the main loop, so that we can set our TargetCreated event. A bunch of debugger callbacks to trigger
                 * when we call WaitForEvent with 0 timeout. However, it seems when we go to do the "real" wait, and we've non-invasively attached, dbgeng!LiveUserTargetInfo::WaitInitialize
                 * is not very happy for some reason, which causes us to get an error. Thus, we have no choice but to set our TargetCreated event inside of the loop. This will be an issue if
                 * we allow launching the target without breaking initially, but we'll have to deal with that when that issue arises (currently we always break on the loader/attach breakpoint) */

                while (!IsEngineCancellationRequested)
                {
                    try
                    {
                        //Go to sleep and wait for an event to occur. We can force the debugger to wake up
                        //via our UiClient by calling DebugControl.SetInterrupt()
                        EngineClient.Control.WaitForEvent(DEBUG_WAIT.DEFAULT, Kernel32.INFINITE);
                    }
                    catch (NullReferenceException ex)
                    {
                        Debug.Assert(false, $"A {ex.GetType().Name} occurred while waiting for a DbgEng debug event. This most likely indicates an issue with a DbgHelp hook. The DbgEng engine lock most likely is still being held; any future attempts at interacting with DbgEng will hang");

                        throw;
                    }

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
            finally
            {
                //Do our best to cleanup. Specifically, we want to call DiscardTarget() so that globals like g_EngStatus
                //are guaranteed to get set back to 0
                EngineClient.TryEndSession(DEBUG_END.ACTIVE_TERMINATE);
            }
        }

        /// <summary>
        /// The input loop of the engine thread. This method runs whenever the debuggee is broken into, and does
        /// not return into the debuggee is resumed.
        /// </summary>
        private void InputLoop()
        {
            //We signal the break event here, rather than in the event callback, so that we can signify that we're 100% input
            //for events and have emitted out current state as well
            Session.BreakEvent.SetResult();

            while (!IsEngineCancellationRequested && Session.Status == EngineStatus.Break)
            {
                //Go to sleep and wait for a command to be enqueued on the UI thread. We will wake up
                //when the UiClient calls DebugClient.ExitDispatch()
                EngineClient.DispatchCallbacks(Kernel32.INFINITE);

                //Similar to breakint out of the EngineLoop, when we want to end the debugger session we'll cancel our CTS and then attempt to call
                //DebugClient.ExitDispatch(). 
                if (IsEngineCancellationRequested)
                    break;

                //Process any commands that were dispatched to the engine thread
                Session.EngineThread.Dispatcher.DrainQueue();
            }

            Session.BreakEvent.Reset();
        }

        private unsafe HRESULT TryCreateProcess(LaunchTargetOptions options)
        {
            /* We have a bit of a problem. We want to be able to launch new processes minimized for ease of debugging. However, DbgEng
             * does not expose a mechanism for us to do this. We can always start the process suspended and *then* attach, but then
             * we've got a new issue: the loader breakpoint won't have occurred yet, and attaching to a process also causes an attach
             * breakpoint to occur. Thus, we're going to end up breaking twice, which is not what we want. There is no combination of DEBUG_ATTACH
             * flags that will do what we want. In order to get the desired behavior, DbgEng *must* be the one to create the process. Thus, we have
             * no choice but to go with plan B: intercept DbgEng calling CreateProcess, and add in the required startup options ourself! */

            try
            {
                //This _will_ execute prior to CreateProcessAndAttach() returning. While DbgEng may create a "pending process" in its bookkeeping, the actual
                //process does exist
                services.DbgEngNativeLibraryLoadCallback.HookCreateProcess = ctx =>
                {
                    var si = (STARTUPINFOW*) ctx.Arg<IntPtr>("lpStartupInfo");

                    //Specifies that CreateProcess should look at the settings specified in wShowWindow
                    si->dwFlags = STARTF.STARTF_USESHOWWINDOW;

                    //We use ShowMinNoActive here instead of ShowMinimized, as ShowMinimized has the effect of causing our debugger
                    //window to flash, losing and then regaining focus. If we never active the newly created process, we never lose
                    //focus to begin with
                    si->wShowWindow = ShowWindow.ShowMinNoActive;

                    /* If you specify DEBUG_PROCESS or DEBUG_ONLY_THIS_PROCESS,
                     * a debug object will be created immediately in CreateProcessInternalW,
                     * after which you won't need to attach to the process manually. */
                    var result = ctx.InvokeOriginal();

                    if ((bool) result)
                    {
                        var pi = ctx.Arg<PROCESS_INFORMATION>("lpProcessInformation");

                        var is32Bit = Kernel32.IsWow64ProcessOrDefault(pi.hProcess);

                        Session.Processes.Add(new DbgEngProcess(Session, services, pi.dwProcessId, is32Bit, options.CommandLine));
                    }

                    return result;
                };

                /* There's no need to create the process suspended. Debug events won't be dispatched until we start calling WaitForEvent.
                 *
                 * There is a 500ms delay the first time this is called, which seems to be related to the DbgEng Extension Gallery.
                 * Debugger::ExtensionGallery::Internal::BackEndRepositoryCache::Connect creates a thread whose thread proc is
                 * Debugger::ExtensionGallery::Internal::BackEndRepositoryCache::ReadLocalDataThreadProc. And then, some genius decided it would be
                 * a good idea to wait 500ms each time it polls to see if the thread has completed.
                 *
                 * This 500ms delay seems to occur each time we launch a new debug target. We fix this by hooking
                 * kernel32!Sleep and reducing the sleep amount */
                var result = EngineClient.TryCreateProcessAndAttachWide(
                    0, options.CommandLine,
                    DEBUG_CREATE_PROCESS.CREATE_NEW_CONSOLE | (options.DebugChildProcesses ? DEBUG_CREATE_PROCESS.DEBUG_PROCESS : DEBUG_CREATE_PROCESS.DEBUG_ONLY_THIS_PROCESS),
                    0,
                    DEBUG_ATTACH.DEFAULT
                );

                return result;
            }
            finally
            {
                services.DbgEngNativeLibraryLoadCallback.HookCreateProcess = null;
            }
        }

        private HRESULT TryAttachProcess(LaunchTargetOptions options)
        {
            var attachFlags = DEBUG_ATTACH.DEFAULT;

            if (options.IsAttach && options.NonInvasive)
            {
                //dbgeng!InvalidAttachFlags() will prevent specifying NONINVASIVE and EXISTING at the same time

                attachFlags |= DEBUG_ATTACH.NONINVASIVE;

                if (options.NoSuspend)
                    attachFlags |= DEBUG_ATTACH.NONINVASIVE_NO_SUSPEND;
            }

            var process = Process.GetProcessById(options.ProcessId);

            var is32Bit = Kernel32.IsWow64ProcessOrDefault(process.Handle);

            Session.Processes.Add(new DbgEngProcess(Session, services, options.ProcessId, is32Bit, null));

            //There's no need to suspend threads prior to attaching. Debug events won't be dispatched until we start calling
            //WaitForEvent
            var hr = EngineClient.TryAttachProcess(0, options.ProcessId, attachFlags);

            return hr;
        }

        private HRESULT TryOpenDump(LaunchTargetOptions options)
        {
            //Not sure how to determine whether a dump is 32-bit or not in a way that would work for any kind of dump file + TTD traces.
            //DbgEng looks at the headers of the dump to determine this, but I don't know what those headers are, so for now let's just assume
            //it matches the bitness of whatever our process is. Note sure how, when we open a given dump, we'll know if it was for x86 or x64 though
            Session.Processes.Add(new DbgEngProcess(Session, services, default, IntPtr.Size == 4, null));

            var hr = EngineClient.TryOpenDumpFile(options.DumpFile);

#if DEBUG
            if (options.HookTTD)
            {
                var replayEngine = TTD.ReplayEngine;

                TTDTracer.Hook(replayEngine);
            }
#endif

            return hr;
        }

        private HRESULT TryConnectServer(LaunchTargetOptions options)
        {
            var info = options.ServerConnectionInfo;

            if (info.Kind == DbgEngServerKind.Debugger)
            {
                //Already connected in CreateEngineClient()
                return HRESULT.S_OK;
            }
            else
            {
                Debug.Assert(info.Kind == DbgEngServerKind.ProcessServer);
                Session.EngineClient.ConnectProcessServer(info.ClientConnectionString);
                throw new NotImplementedException("Handling a process server is not implemented. We need to both store the process server handle, and use it to then launch or attach to a process. It's not like a Debugger Server where you're just connecting to a server; you actually have to launch or attach to a process");
            }
        }
    }
}
