using System.Management.Automation;
using ChaosDbg.DbgEng;

namespace ChaosDbg.PowerShell.Cmdlets
{
    //Called Get-DbgDataModel instead of Get-DbgModel so that when we do Get-DbgMod and hit tab, we get Module instead of Model
    [Cmdlet(VerbsCommon.Get, "DbgDataModel")]
    public class GetDbgDataModel : ChaosCmdlet
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
