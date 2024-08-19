using System;
using System.Management.Automation;
using ChaosDbg.DbgEng.Server;

namespace ChaosDbg.PowerShell.Cmdlets.Server
{
    [Cmdlet(VerbsCommon.New, "DbgServer", DefaultParameterSetName = ParameterSet.NamedPipe)]
    public class NewDbgServer : DbgEngCmdlet
    {
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.COM)]
        public int BaudRate { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.COM)]
        public int Channel { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SecurePipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SSL)]
        public string CertUser { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.TCP)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SSL)]
        public DbgEngServerConnectionMode? CliCon { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SecurePipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SSL)]
        public string MachUser { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.NamedPipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.TCP)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.COM)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SecurePipe)]
        public bool Hidden { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.NamedPipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.TCP)]
        public SwitchParameter IcfEnable { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.TCP)]
        public string IPVersion { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.NamedPipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.TCP)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.COM)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SecurePipe)]
        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.SSL)]
        public string Password { get; set; }

        [Parameter(Mandatory = false, ParameterSetName = ParameterSet.NamedPipe)] //Optional so that NamedPipe can be the default
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.SecurePipe)]
        public string PipeName { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.TCP)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.COM)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.SSL)]
        public string Port { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.SecurePipe)]
        [Parameter(Mandatory = true, ParameterSetName = ParameterSet.SSL)]
        public string Protocol { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter ProcessServer { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Wait { get; set; }

        protected override void ProcessRecord()
        {
            var remoteProvider = GetService<DbgEngRemoteClientProvider>();

            var options = GetOptions();

            if (ProcessServer)
            {
                throw new NotImplementedException();
            }
            else
            {
                remoteProvider.CreateInProcDebuggerServer(options);

                if (Wait)
                    throw new NotImplementedException();
            }
        }

        private DbgEngServerOptionsModel GetOptions()
        {
            int port;

            switch (ParameterSetName)
            {
                //The default parameter set is named pipe; if no pipe name was given, we'll auto-generate one
                case ParameterSet.NamedPipe:
                    return new DbgEngServerNamedPipeOptions(PipeName ?? $"ChaosDbg_{Guid.NewGuid():N}")
                    {
                        Hidden = Hidden,
                        Password = Password,
                        IcfEnable = IcfEnable
                    };

                case ParameterSet.TCP:
                    if (!int.TryParse(Port, out port))
                        throw new ParameterBindingException($"Parameter set {ParameterSetName} requires that -{nameof(Port)} be convertable to an integer");

                    return new DbgEngServerTcpOptions(port)
                    {
                        CliCon = CliCon,
                        Hidden = Hidden,
                        Password = Password,
                        IPVersion = IPVersion,
                        IcfEnable = IcfEnable
                    };

                case ParameterSet.COM:
                    return new DbgEngServerComPortOptions(Port, BaudRate, Channel)
                    {
                        Hidden = Hidden,
                        Password = Password,
                    };

                case ParameterSet.SecurePipe:
                    return new DbgEngServerSPipeOptions(Protocol, PipeName)
                    {
                        CertUser = CertUser,
                        MachUser = MachUser,
                        Hidden = Hidden,
                        Password = Password,
                    };

                case ParameterSet.SSL:
                    if (!int.TryParse(Port, out port))
                        throw new ParameterBindingException($"Parameter set {ParameterSetName} requires that -{nameof(Port)} be convertable to an integer");

                    return new DbgEngServerSslOptions(Protocol, port)
                    {
                        CertUser = CertUser,
                        MachUser = MachUser,
                        CliCon = CliCon,
                        Password = Password,
                    };

                default:
                    throw new UnknownParameterSetException(ParameterSetName);
            }
        }
    }
}
