namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerNamedPipeOptions : DbgEngServerOptionsModel
    {
        public DbgEngServerNamedPipeOptions(string pipeName) : base(DbgEngServerProtocol.NamedPipe)
        {
            PipeName = pipeName;
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

        //Optional
        public bool IcfEnable
        {
            get => GetOptionalOption(DbgEngServerOptionKind.IcfEnable);
            set => SetOptionalOption(DbgEngServerOptionKind.IcfEnable, value);
        }
    }
}
