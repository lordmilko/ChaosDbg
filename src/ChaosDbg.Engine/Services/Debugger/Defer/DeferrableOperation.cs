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

        //Need a separate lock for interacting with incompleteChildren, as Execute() below takes objLock,
        //but if we're waiting on an asynchronous task, it'll be completed on another thread, which will invoke
        //our OnComplete and deadlock trying to touch objLock
        private object incompleteLock = new object();
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
                    lock (incompleteLock)
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

            lock (incompleteLock)
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
                            for (var i = 0; i < Children.Length; i++)
                            {
                                var child = Children[i];

                                if (i == 0)
                                {
                                    Log.Debug<DeferrableOperation>("Executing {type} subtask of {key}", child.GetType().Name, Key.ToString("X"));
                                    child.Execute(forceSynchronous);
                                }
                                else
                                {
                                    /* If the previous task was an async operation (e.g. SymbolDeferrableSubOperation) and we needed to defer loading symbols to low priority,
                                     * we need to skip processing the next task (which will be the task to load the PEFile, which depends on having already loaded symbol modules).
                                     * We can't check whether we had to defer or not, because if forceSynchronous is false, all DoExecute did was kick off the Task. We might not know
                                     * whether we might need to do a slow operation; it doesn't matter though, because either way it's the AsyncDeferrableSubOperation's responsibility
                                     * to execute its NextOperation when it's ready
                                     * 
                                     * If the previous child was an async operation but doesn't declare itself as a dependency of the child after it, thats ok then, and we can execute
                                     * the next child right away */
                                    if (Children[i - 1] is AsyncDeferrableSubOperation a && !forceSynchronous && a.NextOperation != null)
                                    {
                                        //It's up to this async operation to reschedule itself and execute the operation after it. We can't process any more children
                                        Log.Debug<DeferrableOperation>("Ignoring child {child} and all remaining children as it is dependent upon previous child {previousChild}. This child will be executed when {previousChild} has completed", child, a, a);
                                        break;
                                    }
                                    else
                                    {
                                        Log.Debug<DeferrableOperation>("Executing {type} subtask of {key}", child.GetType().Name, Key.ToString("X"));
                                        child.Execute(forceSynchronous);
                                    }
                                }
                            }

                            lock (incompleteLock)
                            {
                                if (incompleteChildren == 0)
                                {
                                    Status = DeferrableOperationStatus.Completed;
                                    OnCompleted.Invoke(this);
                                }
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
