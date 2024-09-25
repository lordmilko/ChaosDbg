using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.DbgEng.Server;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.Symbols.MicrosoftPdb;
using ClrDebug.DbgEng;
using ClrDebug.DIA;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    public class TestContext : IDisposable
    {
        public CordbEngine CordbEngine { get; internal set; } //Can't rely on getting it from the DebugEngineProvider, because once we've disposed it, all that will remain will be the DbgEngEngine, and we'll get an InvalidCastException

        public CordbProcess Process => CordbEngine.Process;

        public CordbThread ActiveThread => Process.Threads.ActiveThread;

        public CordbFrame CurrentFrame => ActiveThread.EnumerateFrames().First();

        public CordbFrame PreviousFrame => ActiveThread.EnumerateFrames().Skip(1).First();

        public INativeInstruction CurrentInstruction =>
            Process.ProcessDisassembler.Disassemble(ActiveThread.RegisterContext.IP);

        private DebugEngineProvider engineProvider;
        private ManualResetEventSlim breakpointHit;
        private ManualResetEventSlim hadFatalException = new ManualResetEventSlim(false);
        private ManualResetEventSlim engineFailureComplete = new ManualResetEventSlim(false);
        private string moduleName;
        private bool disposed;

        public Lazy<DbgEngEngine> InProcDbgEng { get; set; }

        internal Lazy<DbgEngRemoteClient> OutOfProcDbgEng { get; set; }

        public DebugClient OutOfProcDebugClient => OutOfProcDbgEng.Value.DebugClient;

        private EngineFailureStatus lastFatalStatus;

        public EngineFailureStatus LastFatalStatus
        {
            get => lastFatalStatus;
            set
            {
                lastFatalStatus = value;

                if (value == EngineFailureStatus.ShutdownFailure || value == EngineFailureStatus.ShutdownSuccess)
                    engineFailureComplete.Set();
            }
        }

        private Exception lastFatalException;

        public Exception LastFatalException
        {
            get => lastFatalException;
            set
            {
                //We get told of a null exception when we receive the ShutdownSuccess notification
                if (value == null)
                    return;

                lastFatalException = value;

                hadFatalException.Set();
            }
        }

        public TestContext(DebugEngineProvider engineProvider, string exePath)
        {
            this.engineProvider = engineProvider;

            breakpointHit = new ManualResetEventSlim(false);
            engineProvider.BreakpointHit += (s, e) => breakpointHit.Set();
            engineProvider.ExceptionHit += (s, e) => breakpointHit.Set();

            //cordbEngineProvider.ModuleLoad += (s, e) => Debug.WriteLine($"[ModLoad] {e.Module}");
            //cordbEngineProvider.ModuleUnload += (s, e) => Debug.WriteLine($"[ModUnload] {e.Module}");

            moduleName = Path.GetFileNameWithoutExtension(exePath);
        }

        public void MoveTo(string expr)
        {
            if (!expr.Contains("!"))
                expr = $"{moduleName}!{expr}";

            var addr = Process.Evaluator.Evaluate(expr);
            CordbEngine.CreateNativeBreakpoint(addr);
            CordbEngine.Continue();
            WaitForBreakpoint();
        }

        public void MoveToCall(string expr, int maxInstrsToSearch = 100)
        {
            var found = new List<string>();

            for (var i = 0; i < maxInstrsToSearch; i++)
            {
                try
                {
                    StepOver();
                }
                catch (TimeoutException ex)
                {
                    throw new TimeoutException($"Timed out waiting for breakpoint to be hit. The following functions were encountered during stepping:{Environment.NewLine}{string.Join(Environment.NewLine, found)}", ex);
                }

                var disasm = Process.ProcessDisassembler.Disassemble(ActiveThread.RegisterContext.IP);

                Log.Debug<TestContext>($"    Stepped to {disasm}");

                if (disasm.Instruction.Mnemonic == Mnemonic.Call)
                {
                    if (disasm.TryGetOperand(out var operand) && Process.Symbols.TryGetSymbolFromAddress(operand, SymFromAddrOption.Native, out var result))
                    {
                        //DIA does not do a very good job of showing name/undecorated name properly
                        var name = ((MicrosoftPdbSymbol) result.Symbol).SafeDiaSymbol.GetUndecoratedNameEx(UNDNAME.UNDNAME_NAME_ONLY);

                        if (name == expr)
                            return;

                        if (name.StartsWith("@ILT+"))
                        {
                            //In debug builds, assemblies can be compiled using incremental linking, wherein the existing EXE/DLL file is updated, rather than creating a whole new one.
                            //Function calls within the assembly are then all listed indirectly, via a symbol called @ILT+offset(<target>), which specified the address of a thunk
                            //that will then jump to the real function. Demanglers do not know how to deal with this situation, as it's not part of the normal demangling rules. As such,
                            //we must strip this off ourselves and try and demangle the inner value
                            var start = name.IndexOf('(') + 1;

                            name = name.Substring(start, name.Length - start - 1);

                            name = SymbolProvider.GetUndecoratedName(name, UNDNAME.UNDNAME_NAME_ONLY);

                            if (name == expr)
                                return;
                        }

                        found.Add(name);
                    }
                }
            }

            var str = found.Count == 0 ? "None" : string.Join(", ", found);

            Assert.Fail($"Failed to find an instruction calling '{expr}' within {maxInstrsToSearch} instructions. Calls found: {str}");
        }

        public void StepOver(int count = 1)
        {
            for (var i = 0; i < count; i++)
            {
                CordbEngine.StepOverNative();
                WaitForBreakpoint();
            }
        }

        public void WaitForFatalShutdown()
        {
            engineFailureComplete.Wait();

            Assert.AreEqual(EngineFailureStatus.ShutdownSuccess, lastFatalStatus, $"Engine did not fail gracefully. This indicates a bug. Last exception: {LastFatalException}");
        }

        public void WaitForBreakpoint()
        {
            if (WaitHandle.WaitAny(new WaitHandle[] {breakpointHit.WaitHandle, hadFatalException.WaitHandle}, 10000) == WaitHandle.WaitTimeout)
            {
                if (LastFatalException != null)
                    throw LastFatalException;

                throw new TimeoutException("Timed out waiting for breakpoint to be hit");
            }

            breakpointHit.Reset();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            try
            {
                CordbEngine?.Dispose();
                breakpointHit.Dispose();
                engineFailureComplete?.Dispose();

                if (InProcDbgEng != null && InProcDbgEng.IsValueCreated)
                    InProcDbgEng.Value.Dispose();

                if (OutOfProcDbgEng != null && OutOfProcDbgEng.IsValueCreated)
                    OutOfProcDbgEng.Value.Dispose();

                disposed = true;
            }
            catch (Exception ex)
            {
                Debug.Assert(false, $"An exception occurred while disposing a TestContext. This is illegal. {ex.Message}");
            }
        }
    }
}
