﻿using System;
using System.Collections.Generic;
using System.Threading;
using ChaosLib;

namespace ChaosDbg.Debugger
{
    class DispatcherPriorityQueueWorker : IDisposable
    {
        public DispatcherThread DispatcherThread { get; }

        private object pendingOperationLock = new object();
        private Dictionary<long, DeferrableOperation> pendingOperations = new();

        public DispatcherPriorityQueueWorker(string threadName)
        {
            //As our operations may potentially attempt to log, we _must_ enable logging on the remote thread
            DispatcherThread = new DispatcherThread(threadName, queue: new DispatcherPriorityQueue(), enableLog: true);
            DispatcherThread.Start();
        }

        //we want to have this concept of an "deferredoperation" that may contain one or more sub-operations.
        //when we add an operation, it has either a priority of 0 or 1. a priority of 0 means it gets executed
        //right away. some sibling tasks may have a normal priority however (e.g. we want to load ntdll symbols immediately
        //but defer loading the pe data) so we need to be able to handle that.
        //when

        public void Enqueue(DeferrableOperation parentOperation)
        {
            if (parentOperation.Children.Length == 0)
                throw new NotImplementedException("Cannot enqueue operation: operation does not have any children");

            var needDeferment = false;

            foreach (var child in parentOperation.Children)
            {
                if (child.Priority == 0)
                    child.Execute(forceSynchronous: true);
                else
                    needDeferment = true;
            }

            if (needDeferment)
            {
                parentOperation.OnCompleted = RemovePendingOperation;

                lock (pendingOperationLock)
                {
                    var bookeepingDone = new ManualResetEventSlim(false);

                    //Any async operations that we don't force to run synchronously (e.g. SymbolDeferrableSubOperation) will basically run immediately,
                    //kick off a Task on the thread pool that will actually handle the work of loading symbols, and then return. Thus, any time you try and force
                    //load symbols, a job is probably already executing and we just need to wait for it to finish
                    var dispatcherOperation = DispatcherThread.InvokeAsync(
                        () =>
                        {
                            //I think it's possible for the async operation to complete before we've done our bookkeeping, which may result in us trying to call the OnCompleted method before we've
                            //stored the data that RemovePendingOperation will need to mark the operation as completed
                            bookeepingDone.Wait();
                            parentOperation.Execute(forceSynchronous: false);
                        },
                        priority: 1
                    );

                    parentOperation.DispatcherOperation = dispatcherOperation;

                    pendingOperations.Add(parentOperation.Key, parentOperation);

                    bookeepingDone.Set();
                }
            }
        }

        private void RemovePendingOperation(DeferrableOperation parentOperation)
        {
            lock (pendingOperationLock)
                pendingOperations.Remove(parentOperation.Key);

            //Remove it from the dispatcher queue. If it's already in the process of running, this isn't our problem.
            //The caller will need to decide whether or not they want to wait for the operation to complete
            if (parentOperation.DispatcherOperation != null)
                DispatcherThread.Dispatcher.Abort(parentOperation.DispatcherOperation);
        }

        public void ForceExecute(DeferrableOperation parentOperation)
        {
            //If it's in the queue, remove it so it doesn't start running. If it's already running, no problem
            RemovePendingOperation(parentOperation);

            //Now force execute it. If it's already completed, we'll see that there's nothing to do
            parentOperation.Execute(true);

            //If there's any async operations IsCompleted may be true (on the basis we dispatched all the operations), but we now need to force wait for any async operations to complete.
            //Note that we don't want to call WaitForOperation() because that will cause us to wait on the DispatcherOperation, which may be way up the queue
            foreach (var child in parentOperation.Children)
                child.Wait();
        }

        public void ForceExecute(DeferrableSubOperation subOperation) => subOperation.Execute(true);

        public bool TryGetOperation(long key, out DeferrableOperation operation)
        {
            lock (pendingOperationLock)
            {
                if (pendingOperations.TryGetValue(key, out operation))
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Waits for the specified <see cref="DeferrableOperation"/> to naturally be executed on the dispatcher queue, and for any <see cref="AsyncDeferrableSubOperation"/> children to have completed as well.<para/>
        /// If the dispatcher queue is currently blocked, this method will hang until it gets to processing this operation.
        /// </summary>
        /// <param name="parentOperation">The operation to wait on.</param>
        public void WaitForOperation(DeferrableOperation parentOperation)
        {
            //If there's no DispatcherOperation set, this means the entire operation
            //ran synchronously with priority 0 when it was first enqueued, so it's already
            //completed
            if (parentOperation.DispatcherOperation == null)
                return;

            //First, wait for the DispatcherOperation to complete
            parentOperation.DispatcherOperation.Wait();

            //Now wait for any async sub-operations to complete
            foreach (var child in parentOperation.Children)
                child.Wait();
        }

        public void WaitForEmptyQueue()
        {
            do
            {
                lock (pendingOperationLock)
                {
                    if (pendingOperations.Count == 0)
                        return;
                }

                Thread.Sleep(100);
            } while (true);
        }

        public void Abort(DeferrableOperation parentOperation)
        {
            parentOperation.DispatcherOperation.Abort();

            foreach (var child in parentOperation.Children)
                child.Abort();

            WaitForOperation(parentOperation);
        }

        public void Dispose()
        {
            DispatcherThread?.Dispose();
        }
    }
}
