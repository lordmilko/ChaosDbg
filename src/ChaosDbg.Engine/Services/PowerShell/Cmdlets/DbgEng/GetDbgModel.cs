using System.Management.Automation;
using ChaosDbg.DbgEng;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "DbgModel")]
    public class GetDbgModel : ChaosCmdlet
    {
        [Parameter(Mandatory = false)]
        public SwitchParameter Session { get; set; }

        protected override void ProcessRecord()
        {
            if (Session)
            {
                var dataModel = ((DbgEngEngine) ActiveEngine).GetModelSession();

                PSObjectDynamicContainer.EnsurePSObject(dataModel);

                WriteObject(dataModel);
            }
            else
            {
                var dataModel = ((DbgEngEngine) ActiveEngine).GetModelRootNamespace();

                PSObjectDynamicContainer.EnsurePSObject(dataModel);

                WriteObject(dataModel);
            }
        }
    }
}
