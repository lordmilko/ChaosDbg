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

        protected override void ProcessRecord()
        {
            var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();

            dbgEngEngineProvider.EngineFailure += EngineFailure;
            dbgEngEngineProvider.EngineOutput += EngineOutput;

            dbgEngEngineProvider.OpenDump(Path);

            dbgEngEngineProvider.ActiveEngine.WaitForBreak();
        }
    }
}
