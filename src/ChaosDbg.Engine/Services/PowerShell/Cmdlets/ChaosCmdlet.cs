using System;
using System.Management.Automation;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosLib;

namespace ChaosDbg.PowerShell.Cmdlets
{
    public abstract class ChaosCmdlet : PSCmdlet
    {
#pragma warning disable CS0618
        protected IServiceProvider ServiceProvider => InternalGlobalProvider.ServiceProvider;
#pragma warning restore CS0618

        protected T GetService<T>() => ServiceProvider.GetService<T>();

        private IDbgEngine activeEngine;

        protected IDbgEngine ActiveEngine
        {
            get
            {
                if (activeEngine == null)
                {
                    var dbgEngEngineProvider = GetService<DbgEngEngineProvider>();

                    if (dbgEngEngineProvider.ActiveEngine != null)
                        activeEngine = dbgEngEngineProvider.ActiveEngine;
                    else
                    {
                        var cordbEngineProvider = GetService<CordbEngineProvider>();

                        if (cordbEngineProvider.ActiveEngine != null)
                            activeEngine = cordbEngineProvider.ActiveEngine;
                        else
                            throw new NotImplementedException();
                    }
                }

                return activeEngine;
            }
        }

        protected bool PermittedToWrite => CommandRuntime.GetPropertyValue("PipelineProcessor").GetFieldValue("_permittedToWrite") == this;

        protected bool HasParameter(string name) => MyInvocation.BoundParameters.ContainsKey(name);
    }
}
