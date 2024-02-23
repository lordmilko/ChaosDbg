using System;
using System.Threading;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;

namespace chaos
{
    class CordbClient : IDisposable
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private CordbEngineProvider engineProvider;
        private DbgEngEngineProvider dbgEngEngineProvider;
        private RelayParser commandDispatcher;

        private CordbEngine engine => engineProvider.ActiveEngine;

        public CordbClient(
            CordbEngineProvider engineProvider,
            DbgEngEngineProvider dbgEngEngine,
            CommandBuilder commandBuilder)
        {
            this.engineProvider = engineProvider;
            this.dbgEngEngineProvider = dbgEngEngine;
            commandDispatcher = commandBuilder.Build();

            RegisterCallbacks();
        }

        public void Execute(string executable, bool minimized, bool interop)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            engineProvider.CreateProcess(executable, minimized, interop);

            EngineLoop();
        }

        private void EngineLoop()
        {
            while (true)
            {
                wakeEvent.Wait();

                InputLoop();

                wakeEvent.Reset();
            }
        }

        private void InputLoop()
        {
            while (engine.Session.Status == EngineStatus.Break)
            {
                PrintPrompt();

                var command = Console.ReadLine();

                if (!string.IsNullOrEmpty(command))
                    ExecuteCommand(command);
            }
        }

        private void PrintPrompt()
        {
            Console.Write("chaos> ");
        }

        private void ExecuteCommand(string command)
        {
            var parseResult = commandDispatcher.Parse(command);

            if (parseResult.Errors.Count > 0)
                Console.WriteLine($"Invalid command '{command}'");
            else
            {
                commandDispatcher.Invoke(command);
            }
        }

        private void PrintModules()
        {
            foreach (var module in engine.Process.Modules)
            {
                Console.WriteLine(module);
            }
        }

        private void RegisterCallbacks()
        {
            engineProvider.EngineStatusChanged += (s, e) =>
            {
                if (e.NewStatus == EngineStatus.Break)
                    wakeEvent.Set();
            };

            engineProvider.ModuleLoad += (s, e) => Console.WriteLine($"ModLoad: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;
            engineProvider.ModuleUnload += (s, e) => Console.WriteLine($"ModUnload: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;

            engineProvider.ThreadCreate += (s, e) => Console.WriteLine($"ThreadCreate {e.Thread}");
            engineProvider.ThreadExit += (s, e) => Console.WriteLine($"ThreadExit {e.Thread}");
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
