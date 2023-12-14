using System;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class StackTraceCommands
    {
        private CordbEngine engine;

        public StackTraceCommands(CordbEngine engine)
        {
            this.engine = engine;
        }
        
        // [~<id>]k
        [Command("k")]
        public void StackTrace()
        {
            var thread = engine.Process.Threads.ActiveThread;

            foreach (var frame in thread.StackTrace)
                Console.WriteLine(frame);
        }
    }
}
