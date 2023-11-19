using System.Collections.Generic;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbThreadStore
    {
        private Dictionary<int, CordbThread> threads = new Dictionary<int, CordbThread>();

        internal CordbThread Add(CorDebugThread corDebugThread)
        {
            var thread = new CordbThread(corDebugThread);

            threads.Add(corDebugThread.Id, thread);

            return thread;
        }

        internal CordbThread Remove(int id)
        {
            if (threads.TryGetValue(id, out var thread))
                threads.Remove(thread.Id);

            return thread;
        }
    }
}
