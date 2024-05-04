using System;
using System.Diagnostics;
using System.Linq;
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

        private IPEFileProvider peFileProvider;
        private static bool isDbgHelpHooked;

        private static object objLock = new object();

        public bool AllowOverwriteSymbolOpts { get; set; }

        [ThreadStatic]
        public Func<DetourContext, object> HookCreateProcess;

        public DbgEngNativeLibraryLoadCallback(IPEFileProvider peFileProvider)
        {
            this.peFileProvider = peFileProvider;
        }

        public string GetInterestedModule() => WellKnownNativeLibrary.DbgEng;

        public unsafe void NotifyLoad(IntPtr hModule)
        {
            lock (objLock)
            {
                Type[] needFullRepair = null;

                if (isDbgHelpHooked)
                {
                    if (ImportPatcher.TryRepairStaleImportHooks(WellKnownNativeLibrary.DbgEng, (long) (void*) hModule, new[] { typeof(DbgHelp.Native), typeof(Kernel32.Native) }, out needFullRepair))
                        return;

                    Log.Debug<DetourBuilder>("Rehooking DbgEng at new address {address}", hModule.ToString("X"));
                }

                var process = Process.GetCurrentProcess();

                var hProcess = process.Handle;

                var stream = new ProcessMemoryStream(hProcess);

                stream.Position = (long) (void*) hModule;
                var dbgEngPE = peFileProvider.ReadStream(stream, true, PEFileDirectoryFlags.ImportDirectory);

                Log.Debug<DetourBuilder>("Hooking DbgEng {module}", hModule.ToString("X"));

                if (needFullRepair == null || needFullRepair.Contains(typeof(DbgHelp.Native)))
                {
                    ImportPatcher.Patch(
                        peFile: dbgEngPE,
                        importedModule: WellKnownNativeLibrary.DbgHelp,
                        pInvokeProvider: typeof(DbgHelp.Native),
                        userHook: DbgHelpHook, //Ensure that all calls to DbgHelp are protected by our global lock
                        exclude: SafeDbgHelpImports
                    );
                }

                if (needFullRepair == null || needFullRepair.Contains(typeof(Kernel32.Native)))
                {
                    ImportPatcher.Patch(
                        peFile: dbgEngPE,
                        importedModule: null,
                        typeof(Kernel32.Native),
                        Kernel32Hook,
                        include: new[] { "QueueUserAPC", "CreateProcessW" }
                    );
                }

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
                    DbgHelpProvider.Acquire(ctx.Arg<IntPtr>("hProcess")); //todo: if we just created the session, we dont need to increase the refcount twice!
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

                return 0;
            }
            else if (ctx.Name == "CreateProcessW")
            {
                if (HookCreateProcess != null)
                    return HookCreateProcess(ctx);

                return ctx.InvokeOriginal();
            }

            Debug.Assert(false, $"Don't know how to handle {ctx.Name}");
            return ctx.InvokeOriginal();
        }

        public void NotifyUnload(IntPtr hModule)
        {
        }
    }
}
