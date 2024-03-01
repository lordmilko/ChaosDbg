using System;
using System.Diagnostics;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class StackTraceCommands : CommandBase
    {
        public StackTraceCommands(IConsole console, CordbEngineProvider engineProvider) : base(console, engineProvider)
        {
        }
        
        // [~<id>]k
        [Command("k")]
        public void StackTrace()
        {
            var thread = engine.Process.Threads.ActiveThread;

            var sw = new Stopwatch();
            sw.Start();

            Console.WriteLine(" # Child-SP          RetAddr               Call Site");

            for (var i = 0; i < thread.StackTrace.Length; i++)
            {
                var current = thread.StackTrace[i];

                var nextIP = i < thread.StackTrace.Length - 1 ? thread.StackTrace[i + 1].Context.IP : 0;

                if (current is CordbNativeFrame n && n.IsInline)
                    Console.Write($"{i:X2} (InlineFunction) ----------------     ");
                else
                    Console.Write($"{i:X2} {current.Context.SP:x16} {nextIP:x16}     ");

                Console.WriteColorLine(current.ToString(), current is CordbILFrame ? ConsoleColor.Green : null);
            }

            sw.Stop();
            Console.WriteColorLine(sw.Elapsed.ToString(), ConsoleColor.Yellow);
        }
    }
}
