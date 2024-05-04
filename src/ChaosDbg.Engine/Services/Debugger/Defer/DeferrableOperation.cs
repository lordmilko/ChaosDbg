using System;
using System.Diagnostics;
using ChaosLib;

namespace ChaosDbg.Debugger
{
    class DeferrableOperation
    {
        public long Key { get; }

        public DeferrableOperationStatus Status { get; private set; }

        public bool IsCompleted => Status.IsCompleted();

        public DeferrableSubOperation[] Children { get; }

        public DispatcherOperation DispatcherOperation { get; set; }

        public Action<DeferrableOperation> OnCompleted { get; set; }

        private object objLock = new object();

        public DeferrableOperation(long key, params DeferrableSubOperation[] children)
        {
            Key = key;
            Children = children;

            foreach (var child in children)
                child.ParentOperation = this;
        }

        public void Execute(bool forceSynchronous)
        {
            Debug.Assert(OnCompleted != null);

            //See the comments in DeferrableSubOperation for info on why we do this two stage check
            if (!IsCompleted)
            {
                lock (objLock)
                {
                    if (!IsCompleted)
                    {
                        Status = DeferrableOperationStatus.Executing;

                        try
                        {
                            foreach (var child in Children)
                                child.Execute(forceSynchronous);

                            Status = DeferrableOperationStatus.Completed;
                        }
                        catch
                        {
                            Status = DeferrableOperationStatus.Failed;

                            throw;
                        }
                        finally
                        {
                            OnCompleted.Invoke(this);
                        }
                    }
                }
            }
        }
    }
}
