namespace ChaosDbg
{
    public interface IDbgThread
    {
        /// <summary>
        /// Gets the operating system ID of the thread.
        /// </summary>
        int Id { get; }
    }
}
