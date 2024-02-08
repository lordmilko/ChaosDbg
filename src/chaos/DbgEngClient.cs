using System;
using System.Threading;
using ChaosDbg;
using ChaosDbg.DbgEng;
using ClrDebug.DbgEng;

namespace chaos
{
    class DbgEngClient
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private DbgEngEngine engine;

        public void Execute(string executable, bool minimized)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            var engineProvider = GlobalProvider.ServiceProvider.GetService<DbgEngEngineProvider>();

            engine = engineProvider.CreateProcess(executable, minimized, e =>
            {
                e.EngineOutput += Engine_EngineOutput;

                e.EngineStatusChanged += Engine_EngineStatusChanged;
            });

            EngineLoop();
        }

        private void Engine_EngineStatusChanged(object sender, EngineStatusChangedEventArgs e)
        {
            if (e.NewStatus == EngineStatus.Break)
                wakeEvent.Set();
        }

        private void EngineLoop()
        {
            while (true)
            {
                wakeEvent.Wait();

                //An event was received! Output our current state and prompt for user input
                //client.Control.OutputCurrentState(DEBUG_OUTCTL.ALL_CLIENTS, DEBUG_CURRENT.DEFAULT);

                InputLoop();

                wakeEvent.Reset();
            }
        }

        private void InputLoop()
        {
            var client = engine.Session.UiClient;

            while (engine.Target.Status == EngineStatus.Break)
            {
                client.Control.OutputPrompt(DEBUG_OUTCTL.ALL_CLIENTS | DEBUG_OUTCTL.NOT_LOGGED, " ");

                var command = Console.ReadLine();

                //client.Control.OutputPrompt(DEBUG_OUTCTL.ALL_OTHER_CLIENTS, $" {command}\n");

                //If you type the command "?", it's going to say "Hit Enter" to continue. But the thing is, the Execute() method
                //won't return until you hit enter - which means we're now deadlocked! The way you escape this is via the IDebugInputCallbacks.
                //In your input callbacks, you should attempt to get input from the user as normal so that you may end whatever is blocking Execute()
                //and things can get back to normal
                engine.Invoke(c => c.Control.TryExecute(DEBUG_OUTCTL.ALL_CLIENTS, command, DEBUG_EXECUTE.NOT_LOGGED));
            }
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            //todo: my dbgeng client doesnt work at all? it just hangs when we hit g to resume and the process
            //never starts

            //Don't terminate our process!
            e.Cancel = true;

            engine.ActiveClient.Control.SetInterrupt(DEBUG_INTERRUPT.ACTIVE);
        }

        private void Engine_EngineOutput(object sender, EngineOutputEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}
