using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.Detour;
using ChaosLib.Memory;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.DbgEng
{
    class DbgEngNativeLibraryLoadCallback : INativeLibraryLoadCallback
    {
        private static string[] SafeDbgHelpImports =
        {
            "DbgHelpCreateUserDump",
            "FindExecutableImageExW",
            "GetTimestampForLoadedLibrary",
            "ImageDirectoryEntryToDataEx",
            "ImagehlpApiVersionEx",
            "ImageNtHeader",
            "ImageRvaToVa",
            "SetCheckUserInterruptShared",
            "SetSymLoadError",
            "SymAllocDiaString",
            "SymFreeDiaString",
            "SymGetExtendedOption",
            "SymGetHomeDirectoryW",
            "SymGetOptions",
            "SymGetParentWindow",
            "SymMatchFileNameW",
            "SymMatchStringA",
            "SymRegisterGetSourcePathPartCallback",
            "SymRegisterSourceFileUrlListCallback",
            "SymSetExtendedOption",
            "SymSetParentWindow",
        };

        private static bool isDbgHelpHooked;

        private static object objLock = new object();

        private static List<ImportHook> hooks = new List<ImportHook>();

        public bool AllowOverwriteSymbolOpts { get; set; }

        [ThreadStatic]
        public Func<DetourContext, object> HookCreateProcess;

        public string GetInterestedModule() => WellKnownNativeLibrary.DbgEng;

        public unsafe void NotifyLoad(IntPtr hModule)
        {
            lock (objLock)
            {
                Type[] needFullRepair = null;

                if (isDbgHelpHooked)
                {
                    var didRepair = false;

                    foreach (var hook in hooks)
                        didRepair |= hook.Repair(hModule);

                    if (didRepair)
                        InstallAddRefHook();

                    return;
                }

                var process = Process.GetCurrentProcess();

                var hProcess = process.Handle;

                var stream = new ProcessMemoryStream(hProcess);

                stream.Position = (long) (void*) hModule;
                var dbgEngPE = PEFile.FromStream(stream, true, PEFileDirectoryFlags.ImportDirectory);

                Log.Debug<DetourBuilder>("Hooking DbgEng {module}", hModule.ToString("X"));

                var dbgHelpHook = new ImportHook(
                    hModule,
                    dbgEngPE,
                    WellKnownNativeLibrary.DbgHelp,
                    typeof(DbgHelp.Native),
                    DbgHelpHook, //Ensure that all calls to DbgHelp are protected by our global lock
                    exclude: SafeDbgHelpImports
                );

                hooks.Add(dbgHelpHook);

                //These methods are split across imports/API sets, so we need separate hooks

                var kernel32Hook1 = new ImportHook(
                    hModule,
                    dbgEngPE,
                    typeof(Kernel32.Native),
                    Kernel32Hook,
                    include: new[] { "QueueUserAPC", "CreateProcessW" }
                );

                var kernel32Hook2 = new ImportHook(
                    hModule,
                    dbgEngPE,
                    typeof(Kernel32.Native),
                    Kernel32Hook,
                    include: new[] { "Sleep" }
                );

                hooks.Add(kernel32Hook1);
                hooks.Add(kernel32Hook2);

                InstallAddRefHook();
                isDbgHelpHooked = true;
            }
        }

        private object DbgHelpHook(DetourContext ctx)
        {
            try
            {
                Log.Debug<IDbgHelp, IDetourSharedProxy>("Begin intercept {name}", ctx.Name);

                if (!TryHandleSpecialDbgHelpFunction(AllowOverwriteSymbolOpts, ctx, out var result))
                    result = LegacyDbgHelp.WithExternalLock(ctx.InvokeOriginal);

                Log.Debug<IDbgHelp, IDetourSharedProxy>("End intercept {name}", ctx.Name);

                return result;
            }
            catch (Exception ex)
            {
                var message = "Exceptions within a DbgEng callback are not allowed. The DbgEng engine lock will not be released if any callback throws";
                Log.Fatal<IDbgHelp>(ex, message);
                Debug.Assert(false, message);
                throw;
            }
        }

        private unsafe bool TryHandleSpecialDbgHelpFunction(bool allowOverwriteSymbolOpts, DetourContext ctx, out object result)
        {
            result = default;

            switch (ctx.Name)
            {
                //Each call to SymInitialize/SymCleanup increments and decrements a refcount
                //But instead of letting DbgHelp manage the refcount, we manage it ourselves

                case "SymInitialize":
                case "SymInitializeW":
                    dbgHelpProvider.Acquire(ctx.Arg<IntPtr>("hProcess")); //todo: if we just created the session, we dont need to increase the refcount twice!
                    result = true;
                    return true;

                case "SymCleanup":
                    DbgHelpProvider.Release(ctx.Arg<IntPtr>("hProcess")); //todo: if we just released the session ,we dont need to decrease the refcount twice!
                    result = true;
                    return true;

                case "SymSetOptions":
                    {
                        if (allowOverwriteSymbolOpts)
                        {
                            Log.Debug<IDbgHelp, IDetourSharedProxy>($"Allowing DbgEng to set DbgHelp options to {{options}} because {nameof(allowOverwriteSymbolOpts)} was {{allowOverwriteSymbolOpts}}", ctx.Args[0].Value, allowOverwriteSymbolOpts);
                            return false;
                        }
                        else
                        {
                            /* There's 3 places DbgEng will commonly try and set symbols:
                             * 1. During dbgeng!OneTimeInitialization
                             * 2. In dbgeng!SymbolCallbackFunction it will add NO_UNQUALIFIED_LOADS to the existing options
                             * 3. In dbgeng!SymbolCallbackFunction, after doing something with the unqualified loads, restores the original options */

                            Log.Debug<IDbgHelp, IDetourSharedProxy>($"Denying DbgEng from setting DbgHelp options to {{options}} because {nameof(allowOverwriteSymbolOpts)} was {{allowOverwriteSymbolOpts}}", ctx.Args[0].Value, allowOverwriteSymbolOpts);

                            //SymSetOptions returns the current options
                            result = DbgHelp.Native.SymGetOptions();
                            return true;
                        }
                    }
                    break;

                case "SymRegisterCallback":
                    throw new NotImplementedException();

                case "SymRegisterCallback64":
                case "SymRegisterCallbackW64":
                    {
                        var dbgHelp = (LegacyDbgHelp) DbgHelpProvider.Get(ctx.Arg<IntPtr>("hProcess"));

                        Log.Debug<IDbgHelp>($"Storing {ctx.Name} callback for hProcess {{hProcess}}", dbgHelp.hProcess.ToString("X"));

                        //Ensure the dispatcher callback has been registered
                        _ = dbgHelp.Callback;

                        dbgHelp.ExternalCallback = ctx.Arg<PSYMBOL_REGISTERED_CALLBACK64>("CallbackFunction");
                        dbgHelp.ExternalCallbackContext = ctx.Arg<long>("UserContext");
                        result = true;
                        return true;
                    }

                case "SymRegisterFunctionEntryCallback":
                case "SymRegisterFunctionEntryCallback64":
                    {
                        //If we have our own callback registered, and DbgEng tries to overwrite it, that's going to cause issues

                        //It is implied that we have a session due to SymInitialize having acquired one
                        var session = (LegacyDbgHelp) DbgHelpProvider.Get(ctx.Arg<IntPtr>("hProcess"));

                        Log.Debug<IDbgHelp>($"Storing {ctx.Name} callback for hProcess {{hProcess}}", session.hProcess.ToString("X"));

                        //This will also ensure a dispatcher function entry callback is registered (if one isn't registered already)
                        session.ExternalFunctionEntryCallback = ctx.Arg<PSYMBOL_FUNCENTRY_CALLBACK64>("CallbackFunction");
                        session.ExternalFunctionEntryCallbackContext = ctx.Arg<long>("UserContext");
                        result = true;
                        return true;

                        //session.Callback.ExternalCallbackName = ctx.Name;
                        //session.Callback.ExternalCallbackArgs = ctx.GetArgValues();

                        //so i guess we need some sort of global that we can use to get at all existing dbghelp instances, which will remove themselves
                        //from the global when they are disposed. we can then ostensibly install dbgeng's callback onto them. then, withlock() can maybe set a flag
                        //saying that an external caller is currently running, which our callback object will be able to inspect and know to dispatch to dbgeng's
                        //recorded callback, rather than the chaosdbg one. when symcleanup runs, we should ensure that we clear out any dbgeng callbacks recorded
                        //on our callbacks for good measure

                        //We handled it
                        result = true;
                        return true;
                    }

#if DEBUG && DEBUG_STACKWALK
                case "StackWalk2":
                {
                    var frame = (STACKFRAME_EX*) ctx.Arg<IntPtr>("StackFrame");
                    var context = (CROSS_PLATFORM_CONTEXT*) ctx.Arg<IntPtr>("ContextRecord");

                    LogStack(
                        "[Begin] AddrPC: {0:X}, AddrFrame: {1:X}, AddrStack: {2:X}, AddrReturn: {3:X}, RIP: {4:X}, RSP: {5:X}, RBP: {6:X}",
                        frame->AddrPC.Offset,
                        frame->AddrFrame.Offset,
                        frame->AddrStack.Offset,
                        frame->AddrReturn.Offset,
                        context->Amd64Context.Rip,
                        context->Amd64Context.Rsp,
                        context->Amd64Context.Rbp
                    );

                    PGET_MODULE_BASE_ROUTINE64 getModuleBase = (a, b) =>
                    {
                        var original = ctx.Arg<PGET_MODULE_BASE_ROUTINE64>("GetModuleBaseRoutine");

                        var result = original(a, b);

                        LogStack("    [ModuleBase]    Resolved {0:X} to {1:X}", b, result);

                        return result;
                    };

                    result = LegacyDbgHelp.WithExternalLock(() => ctx.InvokeOriginal(
                        ctx.Args[0].Value,
                        ctx.Args[1].Value,
                        ctx.Args[2].Value,
                        ctx.Args[3].Value,
                        ctx.Args[4].Value,
                        ctx.Args[5].Value,
                        ctx.Args[6].Value,
                        //ctx.Args[7].Value,
                        getModuleBase,
                        ctx.Args[8].Value,
                        ctx.Args[9].Value,
                        ctx.Args[10].Value
                    ));

                    LogStack(
                        "[End]   AddrPC: {0:X}, AddrFrame: {1:X}, AddrStack: {2:X}, AddrReturn: {3:X}, RIP: {4:X}, RSP: {5:X}, RBP: {6:X}",
                        frame->AddrPC.Offset,
                        frame->AddrFrame.Offset,
                        frame->AddrStack.Offset,
                        frame->AddrReturn.Offset,
                        context->Amd64Context.Rip,
                        context->Amd64Context.Rsp,
                        context->Amd64Context.Rbp
                    );

                    return true;
                }

                case "SymFunctionTableAccess64":
                {
                    var addrBase = ctx.Arg<long>("AddrBase");
                    result = LegacyDbgHelp.WithExternalLock(ctx.InvokeOriginal);

                    if ((IntPtr) result == IntPtr.Zero)
                        LogStack("    [FunctionTable] Failed to resolve {0:X}", addrBase);
                    else
                        LogStack("    [FunctionTable] Resolved {0:X} to {1:X}", addrBase, ((RUNTIME_FUNCTION*)(IntPtr)result)->BeginAddress);

                    return false;
                }
#endif

                default:
                    return false;
            }
        }

        private object Kernel32Hook(DetourContext ctx)
        {
            if (ctx.Name == "QueueUserAPC")
            {
                /* dbgeng!SendEvent will attempt to notify all clients known to DbgEng about a given event. When a DebugClient belongs to the thread
                 * that is calling WaitForEvent, the event will be dispatched immediately. Otherwise, it will be queued in an APC, which DbgEng will then
                 * wait on before continuing. This is an issue, becuase if the target thread of an APC shuts down before having a chance to execute the APC
                 * (which will only happen when the thread is an "alertable" state, such as while calling WaitForMultipleObjectsEx) then DbgEng will hang
                 * indefinitely. There is no reason we should ever need to notify a DebugClient of anything that isn't the client on the engine thread.
                 * Thus, fail such requests immediately */

                //DbgEng is going to wait for g_EventStatusReady, which is an auto-reset event, thus we need to play nice with the double event system

                //Wait for DbgEng to be ready for us
                var g_EventStatusWaiting = NativeReflector.GetGlobal<IntPtr>("dbgeng!g_EventStatusWaiting");
                Kernel32.WaitForSingleObject(g_EventStatusWaiting, -1);

                var g_EventStatusReady = NativeReflector.GetGlobal<IntPtr>("dbgeng!g_EventStatusReady");

                Kernel32.SetEvent(g_EventStatusReady);

                return 0;
            }
            else if (ctx.Name == "CreateProcessW")
            {
                if (HookCreateProcess != null)
                    return HookCreateProcess(ctx);

                return ctx.InvokeOriginal();
            }
            if (ctx.Name == "Sleep")
            {
                //Fix DbgEng sleeping for 500ms when launching a process while it processes all repository items (which does not take anywhere near 500ms)

                var ms = ctx.Arg<int>("dwMilliseconds");

                if (ms == 500)
                    return ctx.InvokeOriginal(1);

                return ctx.InvokeOriginal();
            }

            Debug.Assert(false, $"Don't know how to handle {ctx.Name}");
            return ctx.InvokeOriginal();
        }

#if DEBUG
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int AddRefDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int AddRefDelegateHook(IntPtr self, AddRefDelegate original);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate int ReleaseDelegateHook(IntPtr self, ReleaseDelegate original);

        private void InstallAddRefHook()
        {
            /*NativeReflector.InstallHook<AddRefDelegate, AddRefDelegateHook>("dbgeng!DebugClient::AddRef", (@this, original) =>
            {
                var newRef = original(@this);

                //Debug.WriteLine("DebugClient 0x{0} AddRef {1} -> {2}", @this.ToString("X"), newRef - 1, newRef);

                return newRef;
            });

            NativeReflector.InstallHook<ReleaseDelegate, ReleaseDelegateHook>("dbgeng!DebugClient::Release", (@this, original) =>
            {
                var newRef = original(@this);

                //Debug.WriteLine("DebugClient 0x{0} Release {1} -> {2}", @this.ToString("X"), newRef + 1, newRef);

                return newRef;
            });*/
        }
#endif

        public void NotifyUnload(IntPtr hModule)
        {
        }

        private void LogStack(string messageTemplate, params object[] propertyValues)
        {
            //ChaosLib.Log.Debug<NativeStackWalker>(messageTemplate, propertyValues);
            //Debug.WriteLine(messageTemplate, propertyValues);
        }
    }
}
