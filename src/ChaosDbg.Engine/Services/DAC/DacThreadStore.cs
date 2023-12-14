using System.Collections.Generic;
using ClrDebug;

namespace ChaosDbg.DAC
{
    public class DacThreadStore
    {
        private DacProvider provider;

        private Dictionary<int, DacpThreadData> threads = new Dictionary<int, DacpThreadData>();
        private object lockObj = new object();

        public DacThreadStore(DacProvider provider)
        {
            this.provider = provider;
        }

        public bool TryGetValue(int threadId, bool refresh, out DacpThreadData data)
        {
            if (refresh)
            {
                //Threads can die way too often, we just can't trust our cache to be reliable
                Refresh();
            }

            lock (lockObj)
            {
                if (threads.TryGetValue(threadId, out data))
                    return true;
            }

            return false;
        }

        public void Refresh()
        {
            lock (lockObj)
            {
                threads.Clear();
                provider.Flush();

                var threadStore = provider.SOS.ThreadStoreData;

                var currentThread = threadStore.firstThread;

                while (currentThread != 0)
                {
                    var threadData = provider.SOS.GetThreadData(currentThread);

                    if (threadData.osThreadId != 0)
                        threads.Add(threadData.osThreadId, threadData); //todo: this crashes when i ctrl+c the process and its in the middle of creating a thread?

                    currentThread = threadData.nextThread;
                }
            }
        }
    }
}
