namespace ChaosDbg
{
    public interface IDbgEngineEventFilter
    {

    }

    public interface IDbgExceptionEventFilter
    {

    }

    public interface IDbgEventFilter
    {
        string Name { get; }
        string Alias { get; }
    }
}
