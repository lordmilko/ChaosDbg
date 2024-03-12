using System;
using System.Linq;
using System.Text;
using ChaosLib;

namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerInfo
    {
        private const string debuggerServerName = "Debugger Server";
        private const string remoteProcessServerName = "Remote Process Server";

        public DbgEngServerKind Kind { get; }

        public StringEnum<DbgEngServerProtocol> Protocol { get; private set; }

        public DbgEngServerOption[] Options { get; private set; }

        public string ConnectionString { get; }

        public DbgEngServerInfo(string queryString)
        {
            if (queryString.StartsWith(debuggerServerName))
                Kind = DbgEngServerKind.Debugger;
            else if (queryString.StartsWith(remoteProcessServerName))
                Kind = DbgEngServerKind.ProcessServer;

            var dash = queryString.IndexOf('-');

            if (dash == -1)
                throw new InvalidOperationException($"Query string '{queryString}' did not contain a dash after the server type");

            queryString = queryString.Substring(dash + 2);

            ParseConnectionString(queryString);

            ConnectionString = CreateConnectionString();
        }

        public DbgEngServerInfo(DbgEngServerKind kind, string connectionString)
        {
            Kind = kind;

            ParseConnectionString(connectionString);

            ConnectionString = CreateConnectionString();
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

            Protocol = protocolName switch
            {
                "npipe" => DbgEngServerProtocol.NamedPipe,
                "tcp" => DbgEngServerProtocol.TCP,
                "com" => DbgEngServerProtocol.COMPort,
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

        public DbgEngServerInfo(DbgEngServerKind kind, StringEnum<DbgEngServerProtocol> protocol, DbgEngServerOption[] options)
        {
            Kind = kind;
            Protocol = protocol;
            Options = options;

            ConnectionString = CreateConnectionString();
        }

        private string CreateConnectionString()
        {
            var builder = new StringBuilder();
            builder.Append(Protocol.StringValue).Append(":");

            if (!Options.Any(o => o.Kind.Value == DbgEngServerOptionKind.Server))
                builder.Append(new DbgEngServerValueOption(DbgEngServerOptionKind.Server, Environment.MachineName)).Append(",");

            for (var i = 0; i < Options.Length; i++)
            {
                builder.Append(Options[i]);

                if (i < Options.Length - 1)
                    builder.Append(",");
            }

            return builder.ToString();
        }

        public override string ToString()
        {
            return ConnectionString;
        }
    }
}
