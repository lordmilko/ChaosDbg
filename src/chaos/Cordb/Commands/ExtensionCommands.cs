using System.Diagnostics;
using System.IO;
using ChaosDbg;
using ChaosLib;
using IConsole = ChaosDbg.IConsole;

namespace chaos.Cordb.Commands
{
    class ExtensionCommands : CommandBase
    {
        public ExtensionCommands(IConsole console, DebugEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        [Command("!windbg")]
        public void AttachWinDbg()
        {
            if (DbgEngResolver.TryGetDbgEngPath(out var path))
            {
                var windbg = Path.Combine(path, "windbg.exe");

                if (!File.Exists(windbg))
                    Error($"Could not find windbg.exe under '{path}'");
                else
                {
                    //We don't want WinDbg to suspend threads, so execute ~*m to resume all threads
                    Process.Start(windbg, $"-pv -p {engine.Process.Id} -c \"~*m\"");
                }
            }
            else
                Error("Could not find the WinDbg installation directory");
        }
    }
}
