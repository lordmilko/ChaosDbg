using System.Management.Automation;

namespace ChaosDbg.PowerShell.Cmdlets.Module
{
    [Cmdlet(VerbsCommon.Get, "DbgModule")]
    public class GetDbgModule : ChaosCmdlet
    {
        protected override void ProcessRecord()
        {
            var modules = ActiveEngine.ActiveProcess.Modules;

            foreach (var module in modules)
                WriteObject(module);
        }
    }
}
