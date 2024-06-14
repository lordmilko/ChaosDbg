using ChaosDbg.DbgEng;

namespace ChaosDbg.PowerShell.Cmdlets
{
    public class DbgEngCmdlet : ChaosCmdlet
    {
        protected new DbgEngEngine ActiveEngine => (DbgEngEngine) base.ActiveEngine;        
    }
}
