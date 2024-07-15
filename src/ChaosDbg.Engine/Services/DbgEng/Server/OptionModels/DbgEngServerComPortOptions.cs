namespace ChaosDbg.DbgEng.Server
{
    class DbgEngServerComPortOptions : DbgEngServerOptionsModel
    {
        public DbgEngServerComPortOptions(string comPort, int baudRate, int comChannel) : base(DbgEngServerProtocol.ComPort)
        {
            ComPort = comPort;
            BaudRate = baudRate;
            ComChannel = comChannel;
        }

        public string ComPort
        {
            get => GetRequiredValue<string>(DbgEngServerOptionKind.Port);
            set => SetRequiredValue(DbgEngServerOptionKind.Port, value);
        }

        public int BaudRate
        {
            get => GetRequiredValue<int>(DbgEngServerOptionKind.Baud);
            set => SetRequiredValue(DbgEngServerOptionKind.Baud, value);
        }

        public int ComChannel
        {
            get => GetRequiredValue<int>(DbgEngServerOptionKind.Channel);
            set => SetRequiredValue(DbgEngServerOptionKind.Channel, value);
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
