using System.Collections.Generic;

namespace ChaosDbg.DbgEng
{
    public class DbgEngThreadStore
    {
        private Dictionary<int, DbgEngThread> threads = new Dictionary<int, DbgEngThread>();
        
        internal DbgEngThread Add(int userId, int systemId)
        {
            var thread = new DbgEngThread(userId, systemId);
            
            //If somehow we already have a thread with the specified system ID, this may
            //potentially indicate a bug and we'd like this to explode
            threads.Add(systemId, thread);
            
            return thread;
        }

        internal DbgEngThread Remove(int systemId)
        {
            if (threads.TryGetValue(systemId, out var thread))
                threads.Remove(thread.SystemId);

            return thread;
        }
    }
}
