namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerTcpOptions : DbgEngServerOptionsModel
    {
        public DbgEngServerTcpOptions(int port) : base(DbgEngServerProtocol.TCP)
        {
            Port = port;
        }

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
        public bool Hidden
        {
            get => GetOptionalOption(DbgEngServerOptionKind.Hidden);
            set => SetOptionalOption(DbgEngServerOptionKind.Hidden, value);
        }

        //Optional
        public string Password
        {
            get => GetOptionalValue<string>(DbgEngServerOptionKind.Password);
            set => SetOptionalValue(DbgEngServerOptionKind.Password, value);
        }

        //Optional
        public string IPVersion
        {
            get => GetOptionalValue<string>(DbgEngServerOptionKind.IPVersion);
            set => SetOptionalValue(DbgEngServerOptionKind.IPVersion, value);
        }

        //Optional
        public bool IcfEnable
        {
            get => GetOptionalOption(DbgEngServerOptionKind.IcfEnable);
            set => SetOptionalOption(DbgEngServerOptionKind.IcfEnable, value);
        }
    }
}
