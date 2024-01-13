namespace ChaosDbg
{
    public class AttachProcessOptions
    {
        public int ProcessId { get; }

        public bool NonInvasive { get; set; }

        public bool NoSuspend { get; set; }

        public bool UseInterop { get; set; }

        public AttachProcessOptions(int processId)
        {
            ProcessId = processId;
        }
    }
}
