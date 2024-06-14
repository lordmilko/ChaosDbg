using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

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
            Task = Task.Run(async () =>
            {
                await DoExecuteAsync(forceSynchronous);

                Status = DeferrableOperationStatus.Completed;
                RaiseOnComplete();

                wait.Set();

                //If we're loading symbols at low priority, we won't allow processing the next operation automatically, and will execute
                //it ourselves when we're ready
                if (!forceSynchronous && ProcessNextOperation)
                    NextOperation?.Execute(false);
            }, CancellationToken);
        }

        protected abstract Task DoExecuteAsync(bool forceSynchronous);

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
