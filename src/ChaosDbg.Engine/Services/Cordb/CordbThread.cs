using ClrDebug;

namespace ChaosDbg.Cordb
{
    class CordbThread : IDbgThread
    {
        public int Id { get; }

        public CordbThread(CorDebugThread corDebugThread)
        {
            Id = corDebugThread.Id;
        }

        public override string ToString() => Id.ToString();
    }
}
