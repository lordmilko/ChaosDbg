using System.Management.Automation;
using ChaosDbg.DbgEng.Server;

namespace ChaosDbg.PowerShell.Cmdlets.Server
{
    [Cmdlet(VerbsCommunications.Connect, "DbgServer")]
    public class ConnectDbgServer : LaunchDebugTargetCmdlet
    {
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.Default, ValueFromPipeline = true)]
        public DbgEngServerConnectionInfo ConnectionInfo { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.Manual, Position = 0)]
        public string ConnectionString { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter ProcessServer { get; set; }

        protected override void ProcessRecord()
        {
            switch (ParameterSetName)
            {
                case ParameterSet.Default:
                    ConnectServer(ConnectionInfo);
                    break;

                case ParameterSet.Manual:
                    ConnectServer(new DbgEngServerConnectionInfo(ProcessServer ? DbgEngServerKind.ProcessServer : DbgEngServerKind.Debugger, ConnectionString));
                    break;

                default:
                    throw new UnknownParameterSetException(ParameterSetName);
            }
        }

        private void ConnectServer(DbgEngServerConnectionInfo connectionInfo)
        {
            var engineProvider = GetService<DebugEngineProvider>();

            engineProvider.EngineFailure += EngineFailure;
            engineProvider.EngineOutput += EngineOutput;

            engineProvider.DbgEng.ConnectServer(connectionInfo);
        }
    }
}
