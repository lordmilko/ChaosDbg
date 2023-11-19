using System;
using System.Threading;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos
{
    class CordbClient
    {
        public void Execute(string executable)
        {
            var engine = GlobalProvider.ServiceProvider.GetService<CordbEngine>();

            RegisterCallbacks(engine);

            engine.Launch(executable);

            while (true)
                Thread.Sleep(1);
        }

        private void RegisterCallbacks(CordbEngine engine)
        {
            engine.ModuleLoad += (s, e) => Console.WriteLine($"ModLoad: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;
            engine.ModuleUnload += (s, e) => Console.WriteLine($"ModUnload: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;

            engine.ThreadCreate += (s, e) => Console.WriteLine($"ThreadCreate {e.Thread}");
            engine.ThreadExit += (s, e) => Console.WriteLine($"ThreadExit {e.Thread}");
        }
    }
}
