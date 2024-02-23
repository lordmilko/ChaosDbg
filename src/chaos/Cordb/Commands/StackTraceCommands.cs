using System;
using System.Diagnostics;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class StackTraceCommands
    {
        private readonly CordbEngineProvider engineProvider;
        private CordbEngine engine => engineProvider.ActiveEngine;

        public StackTraceCommands(CordbEngineProvider engineProvider)
        {
            this.engineProvider = engineProvider;
        }
        
        // [~<id>]k
        [Command("k")]
        public void StackTrace()
        {
            var thread = engine.Process.Threads.ActiveThread;

            var sw = new Stopwatch();
            sw.Start();

            foreach (var frame in thread.StackTrace)
                WriteColor(frame.ToString(), frame is CordbILFrame ? ConsoleColor.Green : null);

            sw.Stop();
            WriteColor(sw.Elapsed.ToString(), ConsoleColor.Yellow);
        }

        private void WriteColor(string text, ConsoleColor? color)
        {
            if (color == null)
                Console.WriteLine(text);
            else
            {
                var original = Console.ForegroundColor;

                try
                {
                    Console.ForegroundColor = color.Value;

                    Console.WriteLine(text);
                }
                finally
                {
                    Console.ForegroundColor = original;
                }
            }
        }
    }
}
