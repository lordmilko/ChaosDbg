using System.Management.Automation;
using ChaosDbg.Cordb;

namespace ChaosDbg.PowerShell.Cmdlets.Control
{
    [Alias("Continue-DbgProcess", "g")]
    [Cmdlet(VerbsLifecycle.Resume, "DbgProcess")]
    public class ResumeDbgProcess : ChaosCmdlet
    {
        protected override void ProcessRecord()
        {
            var engine = GetService<DebugEngineProvider>().ActiveEngine;

            ((CordbEngine) engine).Continue();
        }
    }
}
