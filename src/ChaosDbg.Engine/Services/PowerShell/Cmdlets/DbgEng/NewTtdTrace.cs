using System.Management.Automation;
using ChaosLib;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.New, "TtdTrace")]
    public class NewTtdTrace : ChaosCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public int ProcessId { get; set; }

        protected override void ProcessRecord()
        {
            ttd.Attach(ProcessId);
        }
    }
}
