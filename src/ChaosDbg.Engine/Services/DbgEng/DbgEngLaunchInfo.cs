namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Specifies information about a target that should be debugged using <see cref="DbgEngEngine"/>.
    /// </summary>
    public class DbgEngLaunchInfo
    {
        public string ProcessName { get; }

        public bool StartMinimized { get; set; }

        public DbgEngLaunchInfo(string processName)
        {
            ProcessName = processName;
        }
    }
}
