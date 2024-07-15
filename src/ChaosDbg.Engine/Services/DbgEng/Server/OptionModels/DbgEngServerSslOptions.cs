namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerSslOptions : DbgEngServerOptionsModel
    {
        public DbgEngServerSslOptions(string protocol, int port) : base(DbgEngServerProtocol.SSL)
        {
            Protocol = protocol;
            Port = port;
        }

        public string Protocol
        {
            get => GetRequiredValue<string>(DbgEngServerOptionKind.Proto);
            set => SetRequiredValue(DbgEngServerOptionKind.Proto, value);
        }

        public string CertUser
        {
            get => GetOptionalValue<string>(DbgEngServerOptionKind.CertUser);
            set => SetOptionalValue(DbgEngServerOptionKind.CertUser, value);
        }

        public string MachUser
        {
            get => GetOptionalValue<string>(DbgEngServerOptionKind.MachUser);
            set => SetOptionalValue(DbgEngServerOptionKind.MachUser, value);
        }

        //Required
        public int Port
        {
            get => GetRequiredValue<int>(DbgEngServerOptionKind.Port);
            set => SetRequiredValue(DbgEngServerOptionKind.Port, value);
        }

        public DbgEngServerConnectionMode? CliCon
        {
            get => GetOptionalValue<DbgEngServerConnectionMode?>(DbgEngServerOptionKind.CliCon);
            set => SetOptionalValue(DbgEngServerOptionKind.CliCon, value);
        }

        //Optional
        public string Password
        {
            get => GetOptionalValue<string>(DbgEngServerOptionKind.Password);
            set => SetOptionalValue(DbgEngServerOptionKind.Password, value);
        }
    }
}
