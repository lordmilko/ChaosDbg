using System;
using System.Diagnostics;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    class StackTraceCommands : CommandBase
    {
        public StackTraceCommands(IConsole console, DebugEngineProvider engineProvider) : base(console, engineProvider)
        {
        }
        
        // [~<id>]k
        [Command("k")]
        public void StackTrace()
        {
            var thread = engine.Process.Threads.ActiveThread;

            var sw = new Stopwatch();
            sw.Start();

            Console.WriteLine($" # {"SP",-17} {"BP",-17} {"RetAddr",-17}     Call Site");

            for (var i = 0; i < thread.StackTrace.Length; i++)
            {
                var current = thread.StackTrace[i];

                var nextIP = i < thread.StackTrace.Length - 1 ? thread.StackTrace[i + 1].Context.IP : 0;

                if (current is CordbNativeFrame n && n.IsInline)
                    Console.Write($"{i:X2} (Inline Function) ----------------- -----------------     ");
                else
                    Console.Write($"{i:X2} {FormatAddr(current.FrameSP)} {FormatAddr(current.FrameBP)} {FormatAddr(nextIP)}     ");

                Console.WriteColorLine(current.ToString(), current is CordbILFrame ? ConsoleColor.Green : null);
            }

            sw.Stop();
            Console.WriteColorLine(sw.Elapsed.ToString(), ConsoleColor.Yellow);
        }
    }
}
