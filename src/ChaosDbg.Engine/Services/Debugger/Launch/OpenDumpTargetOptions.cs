namespace ChaosDbg
{
    public class OpenDumpTargetOptions : LaunchTargetOptions
    {
        public OpenDumpTargetOptions(string dumpFile) : base(LaunchTargetKind.OpenDump)
        {
            DumpFile = dumpFile;
        }
    }
}
