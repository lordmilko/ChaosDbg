namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerSPipeOptions : DbgEngServerOptionsModel
    {
        public DbgEngServerSPipeOptions(string protocol, string pipeName) : base(DbgEngServerProtocol.SecurePipe)
        {
            Protocol = protocol;
            PipeName = pipeName;
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

        public string PipeName
        {
            get => GetRequiredValue<string>(DbgEngServerOptionKind.Pipe);
            set => SetRequiredValue(DbgEngServerOptionKind.Pipe, value);
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
    }
}
