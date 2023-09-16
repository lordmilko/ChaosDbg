namespace ChaosDbg.Tests
{
    interface IVisitorResultCollection<out T>
    {
        T[] Children { get; }
    }
}