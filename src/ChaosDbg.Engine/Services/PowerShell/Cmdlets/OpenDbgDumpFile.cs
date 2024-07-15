using System.Management.Automation;
using ChaosDbg.DbgEng;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Alias("Open-DbgTraceFile", "Open-DbgTrace", "Open-DbgDump")]
    [Cmdlet(VerbsCommon.Open, "DbgDumpFile")]
    public class OpenDbgDumpFile : LaunchDebugTargetCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Path { get; set; }

#if DEBUG
        [Parameter(Mandatory = false)]
        public SwitchParameter HookTTD { get; set; }
#endif

        protected override void ProcessRecord()
        {
            var engineProvider = GetService<DebugEngineProvider>();

            engineProvider.EngineFailure += EngineFailure;
            engineProvider.EngineOutput += EngineOutput;

            var engine = engineProvider.DbgEng.OpenDump(
                Path
#if DEBUG
                , HookTTD
#endif
            );

            engine.WaitForBreak();
        }
    }
}
