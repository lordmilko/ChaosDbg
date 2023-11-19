namespace ChaosDbg
{
    /// <summary>
    /// Specifies information about a target that should be debugged using an <see cref="IDbgEngine"/>.
    /// </summary>
    public class DbgLaunchInfo
    {
        public string CommandLine { get; }

        public bool StartMinimized { get; set; }

        public DbgLaunchInfo(string commandLine)
        {
            CommandLine = commandLine;
        }
    }
}
