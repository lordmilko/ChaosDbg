using System;
﻿using System.Diagnostics;
using System.Threading;

namespace ChaosDbg.Debugger
{
    public abstract class DeferrableSubOperation
    {
        public int Priority { get; }

        public bool IsCompleted => Status.IsCompleted();

        public DeferrableOperationStatus Status { get; protected set; }

        private object objLock = new object();

        protected ManualResetEventSlim wait = new ManualResetEventSlim(false);

        internal DeferrableOperation ParentOperation { get; set; }

        public DeferrableSubOperation NextOperation { get; set; }

        public event EventHandler OnComplete;

        protected void RaiseOnComplete() => OnComplete?.Invoke(this, EventArgs.Empty);

        protected DeferrableSubOperation(int priority)
        {
            Priority = priority;
        }

        public void Execute(bool forceSynchronous)
        {
            if (!IsCompleted)
            {
                /* The first thread that hits this will enter the lock and actually do the work. If a second thread hits this at the same time, we want them to block and wait for
                 * the operation to complete, because the user may be trying to force this operation, so if we just return immediately, the operation running on the first thread
                 * won't have completed yet. Once the second thread actually enters the lock, we then check again to see if another thread already completed the task first,
                 * and if so we now return, knowing it is complete */

                lock (objLock)
                {
                    if (!IsCompleted)
                    {
                        try
                        {
                            if (Status == DeferrableOperationStatus.Executing)
                            {
                                //If we managed to enter the lock and the status is Executing, this indicates that either it's an async operation (which exited the lock after it kicked things off on a background thread)
                                //or we messed up and forgot to mark this task as completed after we completed it, in which case we should assert

                                Debug.Assert(this is AsyncDeferrableSubOperation);

                                //If we're not forceSynchronous, it's not our job to wait for the async operation to finish. If the caller wants to wait, that's their responsibility
                                if (forceSynchronous)
                                {
                                    WaitIfAsync();

                                    //If we're not marked as completed now, we messed up our bookkeeping somehow
                                    Debug.Assert(IsCompleted);

                                    RaiseOnComplete();
                                }
                            }
                            else
                            {
                                Status = DeferrableOperationStatus.Executing;

                                DoExecute(forceSynchronous);

                                //If we're an async operation, we'll raise OnComplete in our Task
                                if (this is not AsyncDeferrableSubOperation)
                                {
                                    wait.Set();

                                    //If the status was set to something else (e.g. we're set as the NextOperation on another operation that
                                    //we depend on, so are still pending) don't mark the operation as completed
                                    if (Status == DeferrableOperationStatus.Executing)
                                        Status = DeferrableOperationStatus.Completed;
                                        RaiseOnComplete();
                                }
                                }

                                //Some sub-operations may dispatch an async operation (such as symbol loading) and then return immediately, so that we can get multiple symbol loads
                                //going concurrently. If the user came along however and said we need to have this module's symbols _now_, we need to block and wait for the async task to finish
                                if (forceSynchronous)
                                {
                                    WaitIfAsync();

                                    //If we're not marked as completed now, we messed up our bookkeeping somehow
                                    Debug.Assert(IsCompleted);
                                    RaiseOnComplete();
                                }
                            }
                        }
                        catch
                        {
                            Status = DeferrableOperationStatus.Failed;
                            RaiseOnComplete();

                            wait.Set();

                            throw;
                        }
                    }
                }
            }

            //If we're forcing this operation to complete synchronously, we're only after this operation specifically.
            //if we want the operation that depends on us to complete synchronously, we need to wait on that one directly as well
            if (!forceSynchronous && this is not AsyncDeferrableSubOperation)
                NextOperation?.Execute(false);
        }

        protected abstract void DoExecute(bool forceSynchronous);

        public virtual void Abort()
        {
            if (!IsCompleted)
            {
                //If the operation is in the process of executing, we will block here
                lock (objLock)
                {
                    if (!IsCompleted)
                    {
                        Status = DeferrableOperationStatus.Aborted;
                        RaiseOnComplete();
                        wait.Set();
                    }
                }
            }
        }

        public void Wait() => wait.Wait();

        protected virtual void WaitIfAsync()
        {
        }
    }
}
