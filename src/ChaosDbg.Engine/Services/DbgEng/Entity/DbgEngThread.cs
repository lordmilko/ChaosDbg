namespace ChaosDbg.DbgEng
{
    public class DbgEngThread : IDbgThread
    {
        int IDbgThread.Id => SystemId;

        public int UserId { get; }

        public int SystemId { get; }

        public DbgEngThread(int userId, int systemId)
        {
            UserId = userId;
            SystemId = systemId;
        }
    }
}
