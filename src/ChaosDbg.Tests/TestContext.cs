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
using ChaosDbg.Symbol;
using ChaosLib.Metadata;
using ClrDebug.DbgEng;
using ClrDebug.DIA;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    public class TestContext : IDisposable
    {
        public CordbEngine CordbEngine => cordbEngineProvider.ActiveEngine;

        public CordbProcess Process => CordbEngine.Process;

        public CordbThread ActiveThread => Process.Threads.ActiveThread;

        public CordbFrame CurrentFrame => ActiveThread.EnumerateFrames().First();

        public CordbFrame PreviousFrame => ActiveThread.EnumerateFrames().Skip(1).First();

        public INativeInstruction CurrentInstruction =>
            Process.ProcessDisassembler.Disassemble(ActiveThread.RegisterContext.IP);

        private CordbEngineProvider cordbEngineProvider;
        private ManualResetEventSlim breakpointHit;
        private string moduleName;

        public Lazy<DbgEngEngine> InProcDbgEng { get; set; }

        internal Lazy<DbgEngRemoteClient> OutOfProcDbgEng { get; set; }

        public DebugClient OutOfProcDebugClient => OutOfProcDbgEng.Value.DebugClient;

        public TestContext(CordbEngineProvider cordbEngineProvider, string exePath)
        {
            this.cordbEngineProvider = cordbEngineProvider;

            breakpointHit = new ManualResetEventSlim(false);
            void OnBreakpointHit(object sender, EngineBreakpointHitEventArgs e) => breakpointHit.Set();

            cordbEngineProvider.BreakpointHit += OnBreakpointHit;
            cordbEngineProvider.ModuleLoad += (s, e) => Debug.WriteLine($"[ModLoad] {e.Module}");
            cordbEngineProvider.ModuleUnload += (s, e) => Debug.WriteLine($"[ModUnload] {e.Module}");

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
                StepOver();

                var disasm = Process.ProcessDisassembler.Disassemble(ActiveThread.RegisterContext.IP);

                if (disasm.Instruction.Mnemonic == Mnemonic.Call)
                {
                    if (disasm.TryGetOperand(out var operand) && Process.Symbols.TrySymFromAddr(operand, SymFromAddrOption.Native, out var result))
                    {
                        //DIA does not do a very good job of showing name/undecorated name properly
                        var name = ((IUnmanagedSymbol) result).DiaSymbol.GetUndecoratedNameEx(UNDNAME.UNDNAME_NAME_ONLY);

                        if (name == expr)
                            return;

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

        public void WaitForBreakpoint()
        {
            breakpointHit.Wait();
            breakpointHit.Reset();
        }

        public void Dispose()
        {
            CordbEngine?.Dispose();
            breakpointHit.Dispose();

            if (InProcDbgEng.IsValueCreated)
                InProcDbgEng.Value.Dispose();
        }
    }
}
