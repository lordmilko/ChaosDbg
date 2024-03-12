using System;
using System.Diagnostics;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng.Server
{
    /// <summary>
    /// Provides facilities for interacting with a specific DbgEng server.
    /// </summary>
    class DbgEngRemoteClient : IDisposable
    {
        public DebugClient DebugClient { get; }

        public DbgEngServerInfo ServerInfo { get; }

        private Process process;
        private DbgEngRemoteClientProvider provider;
        private BufferOutputCallbacks output = new BufferOutputCallbacks();

        public DbgEngRemoteClient(DebugClient debugClient, DbgEngServerInfo serverInfo, Process process, DbgEngRemoteClientProvider provider)
        {
            DebugClient = debugClient;

            debugClient.OutputCallbacks = output;

            ServerInfo = serverInfo;
            this.process = process;
            this.provider = provider;
        }

        public string[] Execute(string command)
        {
            return output.Capture(
                () => DebugClient.Control.Execute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT)
            );
        }

        public void Dispose()
        {
            DebugClient?.Dispose();

            if (!process.HasExited)
                process.Kill();

            provider.Remove(this);
        }

        public override string ToString()
        {
            return ServerInfo.ToString();
        }
    }
}
