using System;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Threading;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.Disasm;
using ChaosDbg.Metadata;
using SymHelp.Symbols.MicrosoftPdb;

namespace chaos
{
    class CordbClient : IDisposable
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private DebugEngineProvider engineProvider;
        private RelayParser commandDispatcher;
        private CancellationToken cancellationToken;

        private CordbEngine engine => (CordbEngine) engineProvider.ActiveEngine;

        protected IConsole Console { get; }

        private string lastCommand;

        private Stopwatch sw = new Stopwatch();

        public CordbClient(
            IConsole console,
            DebugEngineProvider engineProvider,
            CommandBuilder commandBuilder)
        {
            Console = console;
            this.engineProvider = engineProvider;
            commandDispatcher = commandBuilder.Build();

            RegisterCallbacks();
        }

        public void Execute(string executable, bool minimized, bool interop, FrameworkKind? frameworkKind = null, CancellationToken token = default)
        {
            cancellationToken = token;

            Console.RegisterInterruptHandler(Console_CancelKeyPress);

            engineProvider.EngineFailure += (s, e) => Console.WriteColorLine($"FATAL: {e.Exception}", ConsoleColor.Red);

            sw.Start();

            Console.WriteLine("Launching...");
            engineProvider.Cordb.CreateProcess(executable, minimized, interop, frameworkKind: frameworkKind);

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
                    sw.Stop();

                    PrintPrompt();

                    Console.WriteColorLine(sw.Elapsed, ConsoleColor.Yellow);

                    //There doesn't seem to be a super great way of interrupting the console,
                    //but in the case of our unit tests, our ReadLine is fake anyway so it doesn't matter
                    command = Console.ReadLine();

                    sw.Restart();

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

            engine.Process.Symbols.TryGetSymbolFromAddress(ip, out var symbol);

            Console.WriteLine($"{symbol?.ToString() ?? ip.ToString("X")}:");

            if (symbol?.Module is MicrosoftPdbSymbolModule m)
            {
                var location = m.GetSourceLocation(ip);

                if (location != null)
                    Console.WriteLine(location.Value);
            }

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
