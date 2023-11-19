namespace ChaosDbg
{
    public interface IDbgModule
    {
        long BaseAddress { get; }

        int Size { get; }

        long EndAddress { get; }
    }
}
