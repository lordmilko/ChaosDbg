using System;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Threading;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Metadata;
using ChaosDbg.Symbol;

namespace chaos
{
    class CordbClient : IDisposable
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private CordbEngineProvider engineProvider;
        private DbgEngEngineProvider dbgEngEngineProvider;
        private RelayParser commandDispatcher;
        private CancellationToken cancellationToken;

        private CordbEngine engine => engineProvider.ActiveEngine;

        protected IConsole Console { get; }

        private string lastCommand;

        public CordbClient(
            IConsole console,
            CordbEngineProvider engineProvider,
            DbgEngEngineProvider dbgEngEngine,
            CommandBuilder commandBuilder)
        {
            Console = console;
            this.engineProvider = engineProvider;
            this.dbgEngEngineProvider = dbgEngEngine;
            commandDispatcher = commandBuilder.Build();

            RegisterCallbacks();
        }

        public void Execute(string executable, bool minimized, bool interop, FrameworkKind? frameworkKind = null, CancellationToken token = default)
        {
            cancellationToken = token;

            Console.RegisterInterruptHandler(Console_CancelKeyPress);

            engineProvider.CreateProcess(executable, minimized, interop, frameworkKind);

            EngineLoop();
        }

        private void EngineLoop()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                WaitHandle.WaitAny(new[]{wakeEvent.WaitHandle, cancellationToken.WaitHandle});

                if (cancellationToken.IsCancellationRequested)
                    break;

                InputLoop();

                wakeEvent.Reset();
            }
        }

        private void InputLoop()
        {
            Console.WriteLine("****************** BREAK ******************");

            OutputCurrentInfo();

            while (engine.Session.Status == EngineStatus.Break && !cancellationToken.IsCancellationRequested)
            {
                //If we get any out of band events, we want them to be shown above our prompt

                string command;

                try
                {
                    PrintPrompt();

                    //There doesn't seem to be a super great way of interrupting the console,
                    //but in the case of our unit tests, our ReadLine is fake anyway so it doesn't matter
                    command = Console.ReadLine();

                    if (string.IsNullOrEmpty(command) && !string.IsNullOrEmpty(lastCommand))
                        command = lastCommand;
                }
                finally
                {
                    Console.ExitWriteProtection();
                }

                if (string.IsNullOrEmpty(command))
                    Console.WriteLine();
                else
                {
                    ExecuteCommand(command);
                }
            }
        }

        private void OutputCurrentInfo()
        {
            var ip = engine.Process.Threads.ActiveThread.RegisterContext.IP;

            engine.Process.Symbols.TrySymFromAddr(ip, SymFromAddrOption.Safe, out var symbol);

            Console.WriteLine($"{symbol?.ToString() ?? ip.ToString("X")}:");

            var instr = engine.Process.ProcessDisassembler.Disassemble(ip);
            Console.WriteLine(instr);
        }

        private void PrintPrompt()
        {
            Console.WriteAndProtect("chaos> ");
        }

        private void ExecuteCommand(string command)
        {
            var parseResult = commandDispatcher.Parse(command);

            if (parseResult.Errors.Count > 0)
                Console.WriteLine($"Invalid command '{command}'");
            else
            {
                parseResult.Invoke();
                lastCommand = command;
            }
        }

        private void RegisterCallbacks()
        {
            engineProvider.EngineStatusChanged += (s, e) =>
            {
                if (e.NewStatus == EngineStatus.Break)
                    wakeEvent.Set();
            };

            engineProvider.ModuleLoad += (s, e) =>
            {
                string extra = null;

                if (e.UserContext is true)
                    extra = "[OutOfBand] ";

                Console.WriteLine($"{extra}ModLoad: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}");
            };
            engineProvider.ModuleUnload += (s, e) =>
            {
                string extra = null;

                if (e.UserContext is true)
                    extra = "[OutOfBand] ";

                Console.WriteLine($"{extra}ModUnload: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}");
            };

            engineProvider.ThreadCreate += (s, e) =>
            {
                string extra = null;

                if (e.UserContext is true)
                    extra = "[OutOfBand] ";

                Console.WriteLine($"{extra}ThreadCreate {e.Thread}");
            };
            engineProvider.ThreadExit += (s, e) =>
            {
                string extra = null;

                if (e.UserContext is true)
                    extra = "[OutOfBand] ";

                Console.WriteLine($"{extra}ThreadExit {e.Thread}");
            };
            engineProvider.BreakpointHit += (s, e) =>
            {
                Console.WriteLine($"Hit {e.Breakpoint}");
            };
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            //Don't terminate our process!
            e.Cancel = true;

            engine.Break();
        }

        public void Dispose()
        {
            engineProvider?.Dispose();
            wakeEvent.Dispose();
        }
    }
}
