using System.ComponentModel;

namespace ChaosDbg.DbgEng.Server
{
    enum DbgEngServerProtocol
    {
        Unknown,

        [Description("npipe")]
        NamedPipe,

        [Description("tcp")]
        TCP,

        [Description("com")]
        COMPort,

        [Description("spipe")]
        SecurePipe,

        [Description("ssl")]
        SSL
    }
}
