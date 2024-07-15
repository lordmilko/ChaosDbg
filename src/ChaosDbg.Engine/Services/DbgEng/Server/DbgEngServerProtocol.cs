using System.ComponentModel;

namespace ChaosDbg.DbgEng.Server
{
    public enum DbgEngServerProtocol
    {
        Unknown,

        [Description("npipe")]
        NamedPipe,

        [Description("tcp")]
        TCP,

        [Description("com")]
        ComPort,

        [Description("spipe")]
        SecurePipe,

        [Description("ssl")]
        SSL
    }
}
