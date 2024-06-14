namespace ChaosDbg
{
    public class AttachProcessTargetOptions : LaunchTargetOptions
    {
        public AttachProcessTargetOptions(int processId) : base(LaunchTargetKind.AttachProcess)
        {
            ProcessId = processId;
        }
    }
}
