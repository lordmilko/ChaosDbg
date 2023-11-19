namespace ChaosDbg
{
    /// <summary>
    /// Specifies information about a target that should be debugged using an <see cref="IDbgEngine"/>.
    /// </summary>
    public class DbgLaunchInfo
    {
        public string ProcessName { get; }

        public bool StartMinimized { get; set; }

        public DbgLaunchInfo(string processName)
        {
            ProcessName = processName;
        }
    }
}
