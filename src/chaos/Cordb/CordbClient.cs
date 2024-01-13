using System;
using System.Threading;
using chaos.Cordb.Commands;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosDbg.Engine;

namespace chaos
{
    class CordbClient : IDisposable
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private CordbEngineProvider engineProvider;
        private CordbEngine engine;
        private RelayParser commandDispatcher;
        private IServiceProvider serviceProvider;

        public CordbClient(CordbEngineProvider engineProvider, CommandBuilder commandBuilder, IServiceProvider serviceProvider)
        {
            this.engineProvider = engineProvider;
            commandDispatcher = commandBuilder.Build();
            this.serviceProvider = serviceProvider;
        }

        public void Execute(string executable, bool minimized, bool interop)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            engine = (CordbEngine) engineProvider.CreateProcess(executable, minimized, interop, initCallback: RegisterCallbacks);

            //This is very dodgy, and we're relying on the premise that we'll never create more than one engine per process
            ((ServiceProvider) serviceProvider).AddSingleton(engine);

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

        private void RegisterCallbacks(ICordbEngine engine)
        {
            engine.EngineStatusChanged += (s, e) =>
            {
                if (e.NewStatus == EngineStatus.Break)
                    wakeEvent.Set();
            };

            engine.ModuleLoad += (s, e) => Console.WriteLine($"ModLoad: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;
            engine.ModuleUnload += (s, e) => Console.WriteLine($"ModUnload: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;

            engine.ThreadCreate += (s, e) => Console.WriteLine($"ThreadCreate {e.Thread}");
            engine.ThreadExit += (s, e) => Console.WriteLine($"ThreadExit {e.Thread}");
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            //Don't terminate our process!
            e.Cancel = true;

            engine.Break();
        }

        public void Dispose()
        {
            engine?.Dispose();
            wakeEvent.Dispose();
        }
    }
}
