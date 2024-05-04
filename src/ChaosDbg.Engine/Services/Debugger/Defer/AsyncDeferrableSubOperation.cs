using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ChaosDbg.Debugger
{
    public abstract class AsyncDeferrableSubOperation : DeferrableSubOperation
    {
        public Task Task { get; protected set; }

        protected CancellationToken CancellationToken { get; }

        public bool ProcessNextOperation { get; protected set; }

        protected AsyncDeferrableSubOperation(int priority, CancellationToken cancellationToken) : base(priority)
        {
            CancellationToken = cancellationToken;
        }

        protected override void DoExecute(bool forceSynchronous)
        {
            Task = Task.Run(async () =>
            {
                await DoExecuteAsync(forceSynchronous);

                Status = DeferrableOperationStatus.Completed;

                wait.Set();

                if (!forceSynchronous && !ProcessNextOperation)
                    NextOperation?.Execute(false);
            }, CancellationToken);
        }

        protected abstract Task DoExecuteAsync(bool forceSynchronous);

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
