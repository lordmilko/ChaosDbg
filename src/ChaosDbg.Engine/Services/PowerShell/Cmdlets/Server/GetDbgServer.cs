using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using ChaosDbg.DbgEng.Server;

namespace ChaosDbg.PowerShell.Cmdlets.Server
{
    [Cmdlet(VerbsCommon.Get, "DbgServer")]
    public class GetDbgServer : DbgEngCmdlet
    {
        [Parameter(Mandatory = false, Position = 0)]
        public string Name { get; set; }

        [Alias("Protocol")]
        [Parameter(Mandatory = false)]
        public DbgEngServerProtocol ServerProtocol { get; set; }

        protected override void ProcessRecord()
        {
            var remoteProvider = GetService<DbgEngRemoteClientProvider>();

            IEnumerable<DbgEngServerConnectionInfo> servers = remoteProvider.GetServers();

            if (HasParameter(nameof(ServerProtocol)))
                servers = servers.Where(v => v.ServerProtocol.Value == ServerProtocol);

            if (HasParameter(nameof(Name)))
            {
                var wildcard = new WildcardPattern(Name, WildcardOptions.IgnoreCase);

                servers = servers.Where(v => wildcard.IsMatch(v.ToString()));
            }

            foreach (var server in servers)
                WriteObject(server);
        }
    }
}
