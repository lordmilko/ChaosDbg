﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ChaosLib;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng.Server
{
    /// <summary>
    /// Provides facilities for creating <see cref="DbgEngRemoteClient"/> instances capable of interacting with remote DbgEng servers.
    /// </summary>
    class DbgEngRemoteClientProvider : IDisposable
    {
        private DbgEngEngineServices services;

        private DebugClient debugClient;
        private BufferOutputCallbacks output = new BufferOutputCallbacks();
        private List<DbgEngRemoteClient> activeClients = new List<DbgEngRemoteClient>();

        private string cdb;

        public DbgEngRemoteClientProvider(DbgEngEngineServices services)
        {
            this.services = services;
            debugClient = services.SafeDebugCreate(false);
            debugClient.OutputCallbacks = output;

            services.EnableTestHook();

            var path = DbgEngResolver.GetDbgEngPath();

            cdb = Path.Combine(path, "cdb.exe");

            if (!File.Exists(cdb))
                throw new FileNotFoundException($"Could not find debug server EXE '{cdb}'");
        }

        public DbgEngServerInfo[] GetServers()
        {
            var rawServers = output.Capture(
                () => debugClient.OutputServers(DEBUG_OUTCTL.THIS_CLIENT, Environment.MachineName, DEBUG_SERVERS.ALL)
            );

            var servers = new DbgEngServerInfo[rawServers.Length];

            for (var i = 0; i < rawServers.Length; i++)
                servers[i] = new DbgEngServerInfo(rawServers[i]);

            return servers;
        }

        public DbgEngRemoteClient CreateDebuggerServer(DbgEngServerProtocol protocol, DbgEngServerOption[] serverOptions, string commandLine)
        {
            KillStaleServers();

            var info = new DbgEngServerInfo(DbgEngServerKind.Debugger, protocol, serverOptions);

            var si = new STARTUPINFOW
            {
                cb = Marshal.SizeOf<STARTUPINFOW>(),

                dwFlags = STARTF.STARTF_USESHOWWINDOW,
                wShowWindow = ShowWindow.ShowMinNoActive
            };

            Kernel32.CreateProcessW($"{cdb} -server {info} {commandLine}", CreateProcessFlags.CREATE_NEW_CONSOLE, IntPtr.Zero, null, ref si, out var pi);

            Kernel32.CloseHandle(pi.hProcess);
            Kernel32.CloseHandle(pi.hThread);

            var process = Process.GetProcessById(pi.dwProcessId);

            if (process == null)
                throw new InvalidOperationException($"Failed to get debug server process {pi.dwProcessId}");

            if (process.HasExited)
                throw new InvalidOperationException($"Debug server process {pi.dwProcessId} exited prematurely");

            DbgEngRemoteClient remoteClient = null;

            process.EnableRaisingEvents = true;
            process.Exited += (s, e) =>
            {
                //When the client is disposed, it will remote itself from our list of active clients
                remoteClient?.Dispose();
            };

            WaitForServerStartup(info);

            try
            {
                var debugClient = services.SafeDebugConnect(info.ToString(), false);

                WaitForTargetInitialize(debugClient);

                remoteClient = new DbgEngRemoteClient(debugClient, info, process, this);

                return remoteClient;
            }
            catch
            {
                process.Kill();

                throw;
            }
        }

        private void WaitForServerStartup(DbgEngServerInfo info)
        {
            var targetPipe = info[DbgEngServerOptionKind.Pipe];

            var servers = GetServers();

            for (var i = 0; i < 20; i++)
            {
                if (servers.Any(s => s[DbgEngServerOptionKind.Pipe] == targetPipe))
                    return;

                Thread.Sleep(100);

                servers = GetServers();
            }

            throw new TimeoutException("Timed out waiting for debug server to become published");
        }

        private void WaitForTargetInitialize(DebugClient client)
        {
            //The server has been published, but DbgEng API requests will return E_UNEXPECTED until the debug target has fully
            //initialized. Spin, attempting to do a basic API request, until it looks like things have initialized

            var systemObjects = client.SystemObjects;

            var i = 0;

            do
            {
                if (systemObjects.TryGetCurrentProcessSystemId(out _) == HRESULT.S_OK)
                    return;

                i++;

                if (i >= 20)
                    break;

                Thread.Sleep(100);
            } while (true);

            throw new TimeoutException("Timed out waiting for debug server target to initialize");
        }

        public DbgEngRemoteClient CreateDebuggerServer(string commandLine)
        {
            return CreateDebuggerServer(
                DbgEngServerProtocol.NamedPipe,
                new DbgEngServerOption[]
                {
                    new DbgEngServerValueOption(DbgEngServerOptionKind.Pipe, $"ChaosDbg_{Guid.NewGuid():N}")
                },
                commandLine
            );
        }

        public DbgEngRemoteClient CreateDebuggerServer(int debuggeePID) =>
            CreateDebuggerServer($"-pv -p {debuggeePID} -c \"~*m\"");

        internal void Remove(DbgEngRemoteClient client) => activeClients.Remove(client);

        private void KillStaleServers()
        {
            var processes = Process.GetProcesses().Where(v => v.ProcessName == "cdb").ToArray();

            foreach (var process in processes)
            {
                var peb = new RemotePeb(process);

                var commandLine = peb.ProcessParameters.CommandLine;

                var args = Shell32.CommandLineToArgvW(commandLine);

                if (args.Length < 4)
                    continue; //Not us

                if (args[1] == "-server" && args[2].StartsWith($"npipe:server={Environment.MachineName},pipe=ChaosDbg_"))
                {
                    //It's a server that we created
                    var info = new DbgEngServerInfo(DbgEngServerKind.Debugger, args[2]);

                    var pipeOption = info[DbgEngServerOptionKind.Pipe];

                    if (!activeClients.Any(c => c.ServerInfo[DbgEngServerOptionKind.Pipe] == pipeOption))
                        process.Kill();
                }
            }
        }

        public void Dispose()
        {
            //Don't dispose NativeLibraryProvider, as other things could be using it

            debugClient?.Dispose();
        }
    }
}