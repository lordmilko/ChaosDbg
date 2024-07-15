using System;
using System.Linq;
using System.Text;
using ChaosLib;

namespace ChaosDbg.DbgEng.Server
{
    public class DbgEngServerConnectionInfo
    {
        private const string debuggerServerName = "Debugger Server";
        private const string remoteProcessServerName = "Remote Process Server";

        public DbgEngServerKind Kind { get; }

        public StringEnum<DbgEngServerProtocol> ServerProtocol { get; private set; }

        public DbgEngServerOption[] Options { get; private set; }

        private string clientConnectionString;

        /// <summary>
        /// Gets the connection string that a client can use to connect to this server.
        /// </summary>
        public string ClientConnectionString
        {
            get
            {
                if (clientConnectionString == null)
                    clientConnectionString = CreateConnectionString(false);

                return clientConnectionString;
            }
        }

        private string serverConnectionString;

        /// <summary>
        /// Gets the connection string that the server used to launch this server.
        /// </summary>
        internal string ServerConnectionString
        {
            get
            {
                if (serverConnectionString == null)
                    serverConnectionString = CreateConnectionString(true);

                return serverConnectionString;
            }
        }

        public DbgEngServerConnectionInfo(string queryString)
        {
            if (queryString.StartsWith(debuggerServerName))
                Kind = DbgEngServerKind.Debugger;
            else if (queryString.StartsWith(remoteProcessServerName))
                Kind = DbgEngServerKind.ProcessServer;
            else
                throw new InvalidOperationException($"Connection string did not specify a debugger kind. Connection string should start with either '{debuggerServerName} - ' or '{remoteProcessServerName} - '.");

            var dash = queryString.IndexOf('-');

            if (dash == -1)
                throw new InvalidOperationException($"Query string '{queryString}' did not contain a dash after the server type");

            queryString = queryString.Substring(dash + 2);

            ParseConnectionString(queryString);
        }

        internal DbgEngServerConnectionInfo(DbgEngServerKind kind, string connectionString)
        {
            Kind = kind;

            ParseConnectionString(connectionString);
        }

        public string this[DbgEngServerOptionKind option]
        {
            get
            {
                var match = Options.SingleOrDefault(o => o.Kind.Value == option);

                if (match == null)
                    return null;

                return ((DbgEngServerValueOption) match).Value;
            }
        }

        private void ParseConnectionString(string str)
        {
            var colon = str.IndexOf(':');

            if (colon == -1)
                throw new InvalidOperationException($"Connection string '{str}' did not contain a protocol followed by a colon");

            var protocolName = str.Substring(0, colon);

            ServerProtocol = protocolName switch
            {
                "npipe" => DbgEngServerProtocol.NamedPipe,
                "tcp" => DbgEngServerProtocol.TCP,
                "com" => DbgEngServerProtocol.ComPort,
                "spipe" => DbgEngServerProtocol.SecurePipe,
                "ssl" => DbgEngServerProtocol.SSL,
                _ => new StringEnum<DbgEngServerProtocol>(DbgEngServerProtocol.Unknown, protocolName)
            };

            str = str.Substring(colon + 1);

            var rawOptions = str.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

            var options = new DbgEngServerOption[rawOptions.Length];

            //Options may or may not have an =
            //https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/activating-a-debugging-server

            for (var i = 0; i < rawOptions.Length; i++)
            {
                var split = rawOptions[i].Split(new[] { "=" }, StringSplitOptions.RemoveEmptyEntries);

                switch (split.Length)
                {
                    case 1:
                        options[i] = new DbgEngServerOption(split[0]);
                        break;

                    case 2:
                        options[i] = new DbgEngServerValueOption(split[0], split[1]);
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle a value with multiple '=' signs ({rawOptions[i]})");
                }
            }

            Options = options;
        }

        internal DbgEngServerConnectionInfo(DbgEngServerKind kind, StringEnum<DbgEngServerProtocol> protocol, DbgEngServerOption[] options)
        {
            Kind = kind;
            ServerProtocol = protocol;
            Options = options;
        }

        private string CreateConnectionString(bool isServer)
        {
            var builder = new StringBuilder();
            builder.Append(ServerProtocol.StringValue).Append(":");

            var opts = Options;

            if (isServer)
            {
                if (Options.Any(o => o.Kind.Value == DbgEngServerOptionKind.Server))
                    opts = opts.Where(v => v.Kind.Value != DbgEngServerOptionKind.Server).ToArray();
            }
            else
            {
                if (!Options.Any(o => o.Kind.Value == DbgEngServerOptionKind.Server))
                    builder.Append(new DbgEngServerValueOption(DbgEngServerOptionKind.Server, Environment.MachineName)).Append(",");
            }

            for (var i = 0; i < opts.Length; i++)
            {
                builder.Append(opts[i]);

                if (i < opts.Length - 1)
                    builder.Append(",");
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ClientConnectionString;
        }
    }
}
