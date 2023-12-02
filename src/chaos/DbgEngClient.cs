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

        public void Execute(string executable, bool minimized)
        {
            var engine = GlobalProvider.ServiceProvider.GetService<DbgEngEngine>();

            engine.EngineOutput += Engine_EngineOutput;

            engine.EngineStatusChanged += Engine_EngineStatusChanged;
            engine.CreateProcess(executable, minimized);

            EngineLoop(engine);
        }

        private void Engine_EngineStatusChanged(object sender, EngineStatusChangedEventArgs e)
        {
            if (e.NewStatus == EngineStatus.Break)
                wakeEvent.Set();
        }

        private void EngineLoop(DbgEngEngine engine)
        {
            while (true)
            {
                wakeEvent.Wait();

                //An event was received! Output our current state and prompt for user input
                //client.Control.OutputCurrentState(DEBUG_OUTCTL.ALL_CLIENTS, DEBUG_CURRENT.DEFAULT);

                InputLoop(engine);

                wakeEvent.Reset();
            }
        }

        private void InputLoop(DbgEngEngine engine)
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
                engine.ExecuteCommand(c => c.Control.TryExecute(DEBUG_OUTCTL.ALL_CLIENTS, command, DEBUG_EXECUTE.NOT_LOGGED));
            }
        }

        private void Engine_EngineOutput(object sender, EngineOutputEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}
