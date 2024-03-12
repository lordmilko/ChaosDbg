namespace ChaosDbg.DbgEng.Server
{
    enum DbgEngServerOptionKind
    {
        Unknown,

        //Parameters
        Pipe,
        Password,
        Port,
        IPVersion,
        CliCon,
        Baud,
        Channel,
        Proto,
        CertUser,
        MachUser,

        //Switches
        Hidden,
        IcfEnable,

        //Client Only
        Server
    }
}
