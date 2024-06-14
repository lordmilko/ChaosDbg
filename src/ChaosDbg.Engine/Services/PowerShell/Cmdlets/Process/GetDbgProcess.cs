using System.Management.Automation;

namespace ChaosDbg.PowerShell.Cmdlets.Process
{
    [Cmdlet(VerbsCommon.Get, "DbgProcess")]
    public class GetDbgProcess : ChaosCmdlet
    {
        protected override void ProcessRecord()
        {
            foreach (var process in ActiveEngine.Session.Processes)
                WriteObject(process);
        }
    }
}
