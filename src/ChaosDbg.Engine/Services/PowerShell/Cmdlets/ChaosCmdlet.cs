using System;
using System.Management.Automation;
using System.Threading;
using ChaosLib;

namespace ChaosDbg.PowerShell.Cmdlets
{
    public abstract class ChaosCmdlet : PSCmdlet
    {
#pragma warning disable CS0618
        protected IServiceProvider ServiceProvider => InternalGlobalProvider.ServiceProvider;
#pragma warning restore CS0618

        /// <summary>
        /// A cancellation token source to use with long running tasks that may need to be interrupted by Ctrl+C.
        /// </summary>
        private readonly CancellationTokenSource TokenSource = new CancellationTokenSource();

        internal CancellationToken CancellationToken => TokenSource.Token;

        protected T GetService<T>() => ServiceProvider.GetService<T>();

        private IDbgEngine activeEngine;

        protected IDbgEngine ActiveEngine
        {
            get
            {
                if (activeEngine == null)
                {
                    var engineProvider = GetService<DebugEngineProvider>();

                    var activeEngine = engineProvider.ActiveEngine;

                    if (activeEngine == null)
                        throw new InvalidOperationException("There is no active debug engine");
                }

                return activeEngine;
            }
        }

        protected override void StopProcessing()
        {
            TokenSource.Cancel();
        }

        internal void Execute() => ProcessRecord();

        protected bool PermittedToWrite => CommandRuntime.GetPropertyValue("PipelineProcessor").GetFieldValue("_permittedToWrite") == this;

        protected bool HasParameter(string name) => MyInvocation.BoundParameters.ContainsKey(name);
    }
}
