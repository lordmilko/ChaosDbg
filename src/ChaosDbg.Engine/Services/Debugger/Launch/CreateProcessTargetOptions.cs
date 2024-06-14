using System.Collections.Specialized;

namespace ChaosDbg
{
    public class CreateProcessTargetOptions : LaunchTargetOptions
    {
        public CreateProcessTargetOptions(string commandLine) : base(LaunchTargetKind.CreateProcess)
        {
            CommandLine = commandLine;

            //Initialize the environment variables dictionary so that we can easily add them against the environment variables
            //property on the options object
            EnvironmentVariables = new StringDictionary();

            StartMinimized = false;
        }
    }
}