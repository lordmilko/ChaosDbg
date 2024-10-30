using System.Management.Automation;

namespace ChaosDbg.PowerShell.Cmdlets.Process
{
    [Alias("Attach-DbgProcess")]
    [Cmdlet(VerbsCommunications.Connect, "DbgProcess")]
    public class AttachDbgProcess : LaunchDebugTargetCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public int Id { get; set; }

        [Parameter(Mandatory = false)]
        public DbgEngineKind Engine { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter NonInvasive { get; set; }

        protected override void ProcessRecord()
        {
            var engineProvider = GetService<DebugEngineProvider>();

            engineProvider.EngineFailure += EngineFailure;
            engineProvider.EngineOutput += EngineOutput;

            var engine = engineProvider.DbgEng.Attach(
                processId: Id,
                nonInvasive: NonInvasive
            );

            engine.WaitForBreak();
        }
    }
}
