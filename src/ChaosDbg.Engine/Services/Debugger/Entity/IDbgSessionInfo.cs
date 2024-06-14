namespace ChaosDbg
{
    public interface IDbgSessionInfo
    {
        IDbgProcessStore Processes { get; }

        IDbgEventFilterStore EventFilters { get; }
    }
}
