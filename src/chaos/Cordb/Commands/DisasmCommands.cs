using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.Disasm;
using ChaosDbg.IL;
using ChaosDbg.Symbol;

#nullable enable

namespace chaos.Cordb.Commands
{
    class DisasmCommands : CommandBase
    {
        private ConsoleDisasmWriter consoleDisasmWriter;

        public DisasmCommands(
            IConsole console,
            CordbEngineProvider engineProvider,
            ConsoleDisasmWriter consoleDisasmWriter) : base(console, engineProvider)
        {
            this.consoleDisasmWriter = consoleDisasmWriter;
        }

        [Command("!u")]
        public void DisasmManaged()
        {
            var activeThread = engine.Process.Threads.ActiveThread;

            var stackTrace = activeThread.StackTrace;

            var topManagedFrame = stackTrace.OfType<CordbILFrame>().FirstOrDefault();

            if (topManagedFrame == null)
                throw new InvalidOperationException("Cannot show managed disassembly: there are no managed frames in the active thread's call stack");

            var function = topManagedFrame.Function;

            var ip = topManagedFrame.CorDebugFrame.IP;

            Console.WriteColorLine(function, ConsoleColor.Magenta);

            foreach (var instr in function.IL)
            {
                if (instr.Offset == ip.pnOffset)
                    Console.Write("* ");

                Console.WriteLine(instr.ToString());
            }
        }

        [Command("u")]
        public void DisasmNative(
            [Argument] string? expr = null)
        {
            INativeInstruction[] disassembly;

            long ip;

            if (expr != null)
            {
                var addr = engine.Process.Evaluator.Evaluate(expr);

                disassembly = engine.Process.ProcessDisassembler.EnumerateInstructions(addr).Take(8).ToArray();

                if (engine.Process.Symbols.TrySymFromAddr(addr, SymFromAddrOption.All, out var sym))
                    Console.WriteColorLine(sym.ToString(), ConsoleColor.Magenta);
                else
                    Console.WriteColorLine(expr, ConsoleColor.Magenta);

                ip = -1;
            }
            else
            {
                var activeThread = engine.Process.Threads.ActiveThread;

                if (activeThread == null)
                {
                    Error("No threads have been loaded");
                    return;
                }

                var frames = activeThread.EnumerateFrames();

                if (engine.Session.IsInterop)
                {
                    //Disassemble the frame at the top of the stack

                    var topFrame = frames.First();

                    ip = topFrame.Context.IP;

                    disassembly = engine.Process.ProcessDisassembler.EnumerateInstructions(ip).Take(8).ToArray();

                    Console.WriteColorLine(topFrame.ToString(), ConsoleColor.Magenta);
                }
                else
                {
                    //When not interop debugging, we only care about IL frames

                    var topFrame = frames.OfType<CordbILFrame>().FirstOrDefault();

                    if (topFrame == null)
                        throw new InvalidOperationException("Cannot show managed disassembly: there are no managed frames in the active thread's call stack");

                    ip = topFrame.Context.IP;

                    disassembly = topFrame.Function.Disassembly;

                    Console.WriteColorLine(topFrame.ToString(), ConsoleColor.Magenta);
                }
            }

            var sw = new Stopwatch();
            sw.Start();

            foreach (var instr in disassembly)
            {
                if (ip == instr.Address)
                    Console.Write("* ");

                engine.Process.ProcessDisassembler.Format(instr, consoleDisasmWriter);
                Console.WriteLine();
            }

            sw.Stop();
            Console.WriteColorLine(sw.Elapsed.ToString(), ConsoleColor.Yellow);
        }

        [Command("!!u")]
        public void DisasmBoth()
        {
            var activeThread = engine.Process.Threads.ActiveThread;

            var stackTrace = activeThread.StackTrace;

            var topFrame = stackTrace.OfType<CordbILFrame>().FirstOrDefault();

            if (topFrame == null)
                throw new InvalidOperationException("Cannot show managed disassembly: there are no managed frames in the active thread's call stack");

            Console.WriteColorLine(topFrame.ToString(), ConsoleColor.Magenta);

            var mapping = topFrame.Function.ILToDisassembly;

            var results = new List<(List<ILInstruction?> il, List<INativeInstruction?> native)>();

            foreach (var item in mapping)
            {
                var il = new List<ILInstruction?>();
                var native = new List<INativeInstruction?>();

                if (item.IL != null)
                    il.AddRange(item.IL);

                if (item.NativeInstructions != null)
                    native.AddRange(item.NativeInstructions);

                while (il.Count < native.Count)
                    il.Add(null);

                while (native.Count < il.Count)
                    native.Add(null);

                results.Add((il, native));
            }

            var widestIL = results.SelectMany(r => r.il).Select(v => v?.ToString().Length ?? 0).Max();

            var strs = new List<List<(ILInstruction? il, INativeInstruction? native, string line)>>();

            foreach (var cur in results)
            {
                var group = new List<(ILInstruction? il, INativeInstruction? native, string line)>();

                for (var j = 0; j < cur.il.Count; j++)
                {
                    var il = cur.il[j];
                    var native = cur.native[j];

                    var left = il?.ToString() ?? string.Empty;
                    var right = native == null ? string.Empty : engine.Process.ProcessDisassembler.Format(native);

                    var line = left.PadRight(widestIL);

                    if (right.Length > 0)
                        line += "    " + right;

                    group.Add((il, native, line));
                }

                strs.Add(group);
            }

            var widestStr = strs.SelectMany(v => v).Max(s => s.line.Length);

            for (var i = 0; i < strs.Count; i++)
            {
                foreach (var item in strs[i])
                {
                    var ilStr = (item.il?.ToString() ?? string.Empty).PadRight(widestIL);
                    Console.Write(ilStr);

                    if (item.native != null)
                    {
                        Console.Write("    ");
                        engine.Process.ProcessDisassembler.Format(item.native, consoleDisasmWriter);
                    }

                    Console.WriteLine();
                }

                if (i < strs.Count - 1)
                    Console.WriteLine(new string('-', widestStr));
            }
        }
    }
}
