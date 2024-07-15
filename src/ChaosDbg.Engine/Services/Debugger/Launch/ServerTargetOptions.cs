using ChaosDbg.DbgEng.Server;

namespace ChaosDbg
{
    public class ServerTargetOptions : LaunchTargetOptions
    {
        public ServerTargetOptions(DbgEngServerConnectionInfo connectionInfo) : base(LaunchTargetKind.Server)
        {
            ServerConnectionInfo = connectionInfo;
        }
    }
}
