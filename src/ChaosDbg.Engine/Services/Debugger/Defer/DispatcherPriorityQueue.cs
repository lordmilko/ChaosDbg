using System;
using System.Threading;
using ChaosDbg.Analysis;
using ChaosLib;

namespace ChaosDbg.Debugger
{
    class DispatcherPriorityQueue : IDispatcherPriorityQueue
    {
        private PriorityQueue<DispatcherOperation, int> priorityQueue = new PriorityQueue<DispatcherOperation, int>();

        public int Count
        {
            get
            {
                lock (objLock)
                    return priorityQueue.Count;
            }
        }

        private object objLock = new object();
        private CountEvent countEvent = new CountEvent();

        public WaitHandle WaitHandle => countEvent.WaitHandle;

        public DispatcherOperation Take()
        {
            if ((TryTake(out var item)))
                return item;

            throw new InvalidOperationException("Attempted to Take when queue was empty.");
        }

        public void Add(DispatcherOperation item, int priority)
        {
            lock (objLock)
            {
                priorityQueue.Enqueue(item, priority);
                countEvent.Add();
            }
        }

        //If we await something, the synchronization context will post a new operation, and we'll land here.
        //Since we only try and load symbols manually in the priority 1 path, set that as the priority here
        public void Add(DispatcherOperation item)
        {
            if (item.Priority == null)
                Add(item, 1);
            else
                Add(item, item.Priority.Value); //When the synchronization context triggers, it'll dispatch a new operation. But just in case we somehow keep our priority, lets try requeue it
        }

        public bool TryTake(out DispatcherOperation item)
        {
            lock (objLock)
            {
                if (!priorityQueue.TryDequeue(out item, out _))
                {
                    item = default;
                    return false;
                }

                countEvent.Set();
                return true;
            }
        }

        public bool Remove(DispatcherOperation item)
        {
            lock (objLock)
            {
                var removed = priorityQueue.Remove(item, out _, out _);

                if (removed)
                    countEvent.Set();

                return removed;
            }
        }
    }
}
