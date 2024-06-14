using System;
using System.Diagnostics;
using System.Text;
using ChaosDbg.Cordb;

namespace ChaosDbg.PowerShell.Cmdlets
{
    public abstract class LaunchDebugTargetCmdlet : ChaosCmdlet
    {
        protected Stopwatch sw = new Stopwatch();

        protected void EngineOutput(object sender, EngineOutputEventArgs e)
        {
            Host.UI.Write(sw.Elapsed + " " + e.Text);
        }

        protected void ExceptionHit(object sender, EngineExceptionHitEventArgs e)
        {
            Host.UI.WriteLine($"Hit exception '{e.Exception}'");
        }

        protected void BreakpointHit(object sender, EngineBreakpointHitEventArgs e)
        {
            if (e.Breakpoint is CordbSpecialBreakpoint s)
            {

            }

            Host.UI.WriteLine($"Hit breakpoint '{e.Breakpoint}'");
        }

        protected void EngineFailure(object sender, EngineFailureEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected void WriteEvent(bool outOfBand, string eventName, string details)
        {
            var builder = new StringBuilder();

            builder.Append(sw.Elapsed);

            var fg = Host.UI.RawUI.ForegroundColor;

            if (outOfBand)
            {
                builder.Append("[OOB] ");

                if (!PermittedToWrite)
                    fg = ConsoleColor.Yellow;
            }
            else
                builder.Append("      ");

            builder.Append(eventName.PadRight(13)).Append(details);

            Host.UI.WriteLine(
                fg,
                Host.UI.RawUI.BackgroundColor,
                builder.ToString()
            );
        }
    }
}
