using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using ChaosLib;

namespace ChaosDbg.Debugger
{
    public abstract class AsyncDeferrableSubOperation : DeferrableSubOperation
    {
        public Task Task { get; protected set; }

        protected CancellationToken CancellationToken => cts.Token;

        public bool ProcessNextOperation { get; protected set; } = true;

        private CancellationTokenSource cts;

        protected AsyncDeferrableSubOperation(int priority, CancellationToken cancellationToken) : base(priority)
        {
            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        protected override void DoExecute(bool forceSynchronous)
        {
            var thread = Thread.CurrentThread;

            Debug.Assert(Log.HasContext(thread));

            Task = Task.Run(async () =>
            {
                using var logContextHolder = new LogContextHolder(thread, CancellationToken);

                var isComplete = await DoExecuteAsync(forceSynchronous);

                /* If we're a SymbolDeferrableSubOperation, we failed to resolve symbols,
                 * and have rescheduled symbol resolution for a low priority wait, we can't allow
                 * this task to be marked as completed just yet. DeferrableOperation.Execute()
                 * may be executing over all of its children, executing each one. If we mark ourselves
                 * as completed, the PEFile task may attempt to run, which is dependent upon the symbol task
                 * having fully completed first. */

                //We _must_ call this, else we might crash when the LogContextHolder is disposed, because DoExecuteAsync returned on a different thread
                logContextHolder.Refresh();

                if (isComplete)
                {
                    Status = DeferrableOperationStatus.Completed;
                    RaiseOnComplete();

                    wait.Set();

                    //If we're loading symbols at low priority, we won't allow processing the next operation automatically, and will execute
                    //it ourselves when we're ready
                    if (!forceSynchronous && ProcessNextOperation)
                        NextOperation?.Execute(false);
                }
            }, CancellationToken);
        }

        protected abstract Task<bool> DoExecuteAsync(bool forceSynchronous);

        public override void Abort()
        {
            cts.Cancel();

            base.Abort();
        }

        protected override void WaitIfAsync()
        {
            try
            {
                Task.Wait(CancellationToken);
            }
            catch (AggregateException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }
}
