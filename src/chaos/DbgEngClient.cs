using System;
using System.Diagnostics;
using ChaosDbg;
using ChaosDbg.DbgEng;
using ClrDebug.DbgEng;

namespace chaos
{
    class DbgEngClient : IDisposable
    {
        private DebugEngineProvider engineProvider;
        private Stopwatch sw = new Stopwatch();

        public DbgEngClient(IConsole console, DebugEngineProvider engineProvider)
        {
            Console = console;
            this.engineProvider = engineProvider;
        }

        private DbgEngEngine engine => (DbgEngEngine) engineProvider.ActiveEngine;

        protected IConsole Console { get; }

        public void Execute(string executable, bool minimized)
        {
            Console.RegisterInterruptHandler(Console_CancelKeyPress);

            engineProvider = GlobalProvider.ServiceProvider.GetService<DebugEngineProvider>();
            engineProvider.EngineOutput += Engine_EngineOutput;

            engineProvider.EngineFailure += (s, e) => Console.WriteColorLine($"FATAL: {e.Exception}", ConsoleColor.Red);

            sw.Start();
            engineProvider.DbgEng.CreateProcess(executable, minimized);

            EngineLoop();
        }

        private void EngineLoop()
        {
            while (true)
            {
                engine.WaitForBreak();

                //An event was received! Output our current state and prompt for user input
                //client.Control.OutputCurrentState(DEBUG_OUTCTL.ALL_CLIENTS, DEBUG_CURRENT.DEFAULT);

                InputLoop();
            }
        }

        private void InputLoop()
        {
            var client = engine.Session.ActiveClient;

            while (engine.Session.Status == EngineStatus.Break)
            {
                client.Control.OutputPrompt(DEBUG_OUTCTL.ALL_CLIENTS | DEBUG_OUTCTL.NOT_LOGGED, " ");

                var command = Console.ReadLine();

                //If you type the command "?", it's going to say "Hit Enter" to continue. But the thing is, the Execute() method
                //won't return until you hit enter - which means we're now deadlocked! The way you escape this is via the IDebugInputCallbacks.
                //In your input callbacks, you should attempt to get input from the user as normal so that you may end whatever is blocking Execute()
                //and things can get back to normal
                engine.Invoke(c => c.Control.TryExecute(DEBUG_OUTCTL.ALL_CLIENTS, command, DEBUG_EXECUTE.NOT_LOGGED));
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            //Don't terminate our process!
            e.Cancel = true;

            engine.ActiveClient.Control.SetInterrupt(DEBUG_INTERRUPT.ACTIVE);
        }

        private void Engine_EngineOutput(object sender, EngineOutputEventArgs e)
        {
            Console.Write(sw.Elapsed + " " + e.Text);
        }

        public void Dispose()
        {
            engineProvider?.Dispose();
        }
    }
}
