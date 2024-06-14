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
        private int incompleteChildren;

        public DeferrableOperation(long key, params DeferrableSubOperation[] children)
        {
            Key = key;
            Children = children;

            foreach (var child in children)
            {
                child.ParentOperation = this;
                child.OnComplete += (s, e) =>
                {
                    lock (objLock)
                    {
                        incompleteChildren--;

                        if (incompleteChildren == 0)
                        {
                            Status = DeferrableOperationStatus.Completed;
                            OnCompleted.Invoke(this);
                        }
                    }
                };
            }

            incompleteChildren = children.Length;
        }

        public void Execute(bool forceSynchronous)
        {
            Log.Debug<DeferrableOperation>("Executing {key} (forceSynchronous: {forceSynchronous})", Key.ToString("X"), forceSynchronous);

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
                            {
                                Log.Debug<DeferrableOperation>("Executing {type} subtask of {key}", child.GetType().Name, Key.ToString("X"));
                                child.Execute(forceSynchronous);
                            }

                            if (incompleteChildren == 0)
                            {
                                Status = DeferrableOperationStatus.Completed;
                                OnCompleted.Invoke(this);
                            }
                        }
                        catch
                        {
                            Status = DeferrableOperationStatus.Failed;
                            OnCompleted.Invoke(this);

                            throw;
                        }
                    }
                    else
                        Log.Debug<DeferrableOperation>("After entering lock, {key} had already completed", Key.ToString("X"));
                }
            }
            else
                Log.Debug<DeferrableOperation>("{key} has already completed", Key.ToString("X"));
        }
    }
}
