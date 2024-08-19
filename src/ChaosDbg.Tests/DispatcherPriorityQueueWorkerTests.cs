using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ChaosDbg.Debugger;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    public class MockDeferrableSubOperation : DeferrableSubOperation
    {
        public Action<bool> ExecuteAction { get; }

        public MockDeferrableSubOperation(Action<bool> executeAction, int priority = 1) : base(priority)
        {
            ExecuteAction = executeAction;
        }

        protected override void DoExecute(bool forceSynchronous) => ExecuteAction(forceSynchronous);
    }

    public class MockAsyncDeferrableSubOperation : AsyncDeferrableSubOperation
    {
        public Func<bool, Task> ExecuteAction { get; }

        public MockAsyncDeferrableSubOperation(Func<bool, Task> executeAction, int priority = 1) : base(priority, default)
        {
            ExecuteAction = executeAction;
        }

        protected override async Task<bool> DoExecuteAsync(bool forceSynchronous)
        {
            await ExecuteAction(forceSynchronous);
            return true;
        }
    }

    [TestClass]
    public class DispatcherPriorityQueueWorkerTests : BaseTest
    {
        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ParentWithTwoChildren_ExecuteNormally()
        {
            //The tasks should naturally get executed on the background thread

            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            var first = new MockDeferrableSubOperation(_ => Thread.Sleep(100));
            var second = new MockAsyncDeferrableSubOperation(async _ => await Task.Delay(100));

            var parent = new DeferrableOperation(key: 1, first, second);

            worker.Enqueue(parent);

            worker.WaitForOperation(parent);
        }

        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ParentWithTwoChildren_ForceFullyExecuted_WhilePending()
        {
            //The operation is blocked by another operation, but we forcefully execute it immediately and then
            //confirm it gets cleaned up from the queue properly

            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            //Create an operation that will block the queue

            var blockEvent = new ManualResetEventSlim(false);

            var blockOp = new MockDeferrableSubOperation(_ =>
            {
                blockEvent.Wait();
            });

            var blockParent = new DeferrableOperation(1, blockOp);

            worker.Enqueue(blockParent);

            var haveForceExecuted = false;

            //Now construct our real operation
            var first = new MockDeferrableSubOperation(_ =>
            {
                //Ensure that we don't try and re-run this after we unblock the queue
                Assert.IsFalse(haveForceExecuted);

                Thread.Sleep(100);
            });
            var second = new MockAsyncDeferrableSubOperation(async _ =>
            {
                //Ensure that we don't try and re-run this after we unblock the queue
                Assert.IsFalse(haveForceExecuted);

                await Task.Delay(100);
            });

            try
            {
                var parent = new DeferrableOperation(key: 2, first, second);

                worker.Enqueue(parent);

                //Wait for the block parent to be executing
                Thread.Sleep(100);

                Assert.IsTrue(blockParent.Status == DeferrableOperationStatus.Executing);

                //Now force execute our operation
                worker.ForceExecute(parent);
                haveForceExecuted = true;
                Assert.IsTrue(parent.IsCompleted);

                blockEvent.Set();
                worker.WaitForOperation(blockParent);
                worker.WaitForEmptyQueue();
            }
            finally
            {
                //Make sure that we set the event, even if an assert fails. Otherwise, we'll deadlock
                //trying to dispose the operation waiting for the event to end
                blockEvent.Set();
            }
        }

        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ForcePartiallyExecuted_WhilePending()
        {
            /* The operation is blocked by another operation, but we forcefully execute one of its operations.
             * The other operation should remain in the queue, and if it tries to run while we're already in
             * the middle of forcefully executing it, it'll see this and wait for us to finish. so we need to somehow
             * simulate this race in a test */

            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            //Create an operation that will block the queue

            var blockEvent = new ManualResetEventSlim(false);

            var blockOp = new MockDeferrableSubOperation(_ =>
            {
                blockEvent.Wait();
            });

            var blockParent = new DeferrableOperation(1, blockOp);

            worker.Enqueue(blockParent);

            //Now construct our real operation
            var firstIsExecuting = new ManualResetEventSlim(false);

            var hasTriedExecutingFirst = false;

            var first = new MockAsyncDeferrableSubOperation(async _ =>
            {
                //We should not try and execute this twice
                Debug.Assert(hasTriedExecutingFirst == false);
                Assert.IsFalse(hasTriedExecutingFirst);
                hasTriedExecutingFirst = true;

                blockEvent.Set();

                firstIsExecuting.Set();

                await Task.Delay(100);
            });

            var second = new MockAsyncDeferrableSubOperation(async _ => await Task.Delay(100));

            var parent = new DeferrableOperation(2, first, second);

            worker.Enqueue(parent);

            try
            {
                first.Execute(false);
                firstIsExecuting.Wait();

                //"first" will now clear the operation blocking the queue
                worker.WaitForOperation(blockParent);

                //Now wait for first to complete, while first is still executing. We should try and run it again,
                //see that we're already in the middle of running it, and wait
                worker.WaitForOperation(parent);

                Assert.IsTrue(parent.IsCompleted);

                foreach (var child in parent.Children)
                    Assert.IsTrue(child.IsCompleted);
            }
            finally
            {
                //Make sure that we set the event, even if an assert fails. Otherwise, we'll deadlock
                //trying to dispose the operation waiting for the event to end
                blockEvent.Set();
            }
        }

        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ParentWithTwoChildren_ForceFullyExecuted_WhileExecuting()
        {
            //We should see that the parent operation is already executing and wait for it to finish

            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            var hasTriedExecutingFirst = false;

            var firstIsExecuting = new ManualResetEventSlim(false);

            var first = new MockDeferrableSubOperation(_ =>
            {
                //We should not try and execute this twice
                Debug.Assert(hasTriedExecutingFirst == false);
                Assert.IsFalse(hasTriedExecutingFirst);
                hasTriedExecutingFirst = true;

                firstIsExecuting.Set();

                Thread.Sleep(100);
            });

            var second = new MockAsyncDeferrableSubOperation(async _ => await Task.Delay(100));

            var parent = new DeferrableOperation(2, first, second);

            worker.Enqueue(parent);

            firstIsExecuting.Wait();

            Assert.IsFalse(parent.IsCompleted);

            //Now try and force execute it. It should see that it's already executing, and wait
            worker.ForceExecute(parent);

            Assert.IsTrue(parent.IsCompleted);

            foreach (var child in parent.Children)
                Assert.IsTrue(child.IsCompleted);
        }

        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ParentWithTwoChildren_ForcePartiallyExecuted_WhileExecuting_SubTaskPending()
        {
            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            var hasTriedExecutingFirst = false;

            var firstIsExecuting = new ManualResetEventSlim(false);
            var continueSecond = new ManualResetEventSlim(false);

            var first = new MockDeferrableSubOperation(_ =>
            {
                //We should not try and execute this twice
                Debug.Assert(hasTriedExecutingFirst == false);
                Assert.IsFalse(hasTriedExecutingFirst);
                hasTriedExecutingFirst = true;

                firstIsExecuting.Set();

                Thread.Sleep(100);
            });

            var second = new MockAsyncDeferrableSubOperation(async _ =>
            {
                continueSecond.Wait();
                await Task.Delay(100);
            });

            var parent = new DeferrableOperation(2, first, second);

            worker.Enqueue(parent);

            firstIsExecuting.Wait();

            worker.ForceExecute(first);
            Assert.IsFalse(second.IsCompleted);
            Assert.IsTrue(first.IsCompleted);
            Assert.IsFalse(parent.IsCompleted, "Expected parent to not be completed");

            continueSecond.Set();
            worker.WaitForOperation(parent);

            Assert.IsTrue(parent.IsCompleted);
            Assert.IsTrue(second.IsCompleted);
        }

        [TestMethod]
        public void DispatcherPriorityQueueWorker_Enqueue_ParentWithTwoChildren_ForcePartiallyExecuted_WhileExecuting_SubTaskAlreadyCompleted()
        {
            using var worker = new DispatcherPriorityQueueWorker("Deferred Queue Worker");

            var hasTriedExecutingFirst = false;

            var secondIsExecuting = new ManualResetEventSlim(false);
            var continueSecond = new ManualResetEventSlim(false);

            var first = new MockDeferrableSubOperation(_ =>
            {
                //We should not try and execute this twice
                Debug.Assert(hasTriedExecutingFirst == false);
                Assert.IsFalse(hasTriedExecutingFirst);
                hasTriedExecutingFirst = true;

                Thread.Sleep(100);
            });

            var second = new MockAsyncDeferrableSubOperation(async _ =>
            {
                secondIsExecuting.Set();
                continueSecond.Wait();
                await Task.Delay(100);
            });

            var parent = new DeferrableOperation(2, first, second);

            worker.Enqueue(parent);

            secondIsExecuting.Wait();

            Assert.IsTrue(first.IsCompleted);
            worker.ForceExecute(first);
            continueSecond.Set();
        }
    }
}
