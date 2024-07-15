using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Host;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Metadata;
using ChaosDbg.PowerShell.Host;
using ChaosDbg.Symbol;
using ChaosLib;
using ChaosLib.Symbols.MicrosoftPdb;

namespace ChaosDbg.PowerShell.Cmdlets.Process
{
    [Cmdlet(VerbsLifecycle.Start, "DbgProcess")]
    public class StartDbgProcess : LaunchDebugTargetCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string CommandLine { get; set; }

        [Parameter(Mandatory = false)]
        public DbgEngineKind Engine { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Minimized { get; set; }
#if DEBUG
            = true;
#endif

        private FrameworkKind? frameworkKind;

        protected override void ProcessRecord()
        {
            var engineKind = GetEngineKind();

            switch (engineKind)
            {
                case DbgEngineKind.DbgEng:
                    StartDbgEng();
                    break;

                case DbgEngineKind.Cordb:
                case DbgEngineKind.Interop:
                    StartCordb();
                    break;

                default:
                    throw new UnknownEnumValueException(engineKind);
            }
        }

        private void StartDbgEng()
        {
            var dbgEngEngineProvider = GetService<DebugEngineProvider>();

            dbgEngEngineProvider.EngineFailure += EngineFailure;
            dbgEngEngineProvider.EngineOutput += EngineOutput;

            sw.Start();
            var engine = dbgEngEngineProvider.DbgEng.CreateProcess(
                commandLine: CommandLine,
                startMinimized: Minimized
            );

            engine.WaitForBreak();
        }

        private void StartCordb()
        {
            var cordbEngineProvider = GetService<DebugEngineProvider>();

            cordbEngineProvider.EngineStatusChanged += EngineStatusChanged;
            cordbEngineProvider.EngineFailure += EngineFailure;
            cordbEngineProvider.EngineOutput += EngineOutput;
            cordbEngineProvider.ThreadCreate += ThreadCreate;
            cordbEngineProvider.ThreadExit += ThreadExit;
            cordbEngineProvider.ModuleLoad += ModuleLoad;
            cordbEngineProvider.ModuleUnload += ModuleUnload;
            cordbEngineProvider.ExceptionHit += ExceptionHit;
            cordbEngineProvider.BreakpointHit += BreakpointHit;

            cordbEngineProvider.Cordb.CreateProcess(
                commandLine: CommandLine,
                startMinimized: Minimized,
                useInterop: Engine == DbgEngineKind.Interop,
                frameworkKind: frameworkKind
            );
        }

        protected void EngineStatusChanged(object sender, EngineStatusChangedEventArgs e)
        {
            if (e.NewStatus == EngineStatus.Break)
                OutputCurrentInfo((CordbEngine) sender);
        }

        protected void ThreadCreate(object sender, EngineThreadCreateEventArgs e)
        {
            WriteEvent(e.UserContext is true, "ThreadCreate", e.Thread.ToString());
        }

        protected void ThreadExit(object sender, EngineThreadExitEventArgs e)
        {
            WriteEvent(e.UserContext is true, "ThreadExit", e.Thread.ToString());
        }

        protected void ModuleLoad(object sender, EngineModuleLoadEventArgs e)
        {
            WriteEvent(e.UserContext is true, "ModuleLoad", GetModulePath(e.Module));
        }

        protected void ModuleUnload(object sender, EngineModuleUnloadEventArgs e)
        {
            WriteEvent(e.UserContext is true, "ModuleUnload", GetModulePath(e.Module));
        }

        private void OutputCurrentInfo(CordbEngine engine)
        {
            var ip = engine.Process.Threads.ActiveThread.RegisterContext.IP;

            engine.Process.Symbols.TrySymFromAddr(ip, SymFromAddrOption.Safe, out var symbol);

            Host.UI.WriteLine($"{symbol?.ToString() ?? ip.ToString("X")}:");

            if (symbol?.Module is MicrosoftPdbSymbolModule m)
            {
                var location = m.GetSourceLocation(ip);

                if (location != null)
                    Host.UI.WriteLine(location.Value.ToString());
            }

            var instr = engine.Process.ProcessDisassembler.Disassemble(ip);
            Host.UI.WriteLine(instr.ToString());
        }

        private DbgEngineKind GetEngineKind()
        {
            if (!HasParameter(nameof(Engine)))
            {
                var detector = GetService<IFrameworkTypeDetector>();

                frameworkKind = detector.Detect(CommandLine);

                switch (frameworkKind.Value)
                {
                    case FrameworkKind.Native:
                        return DbgEngineKind.DbgEng;

                    case FrameworkKind.NetCore:
                    case FrameworkKind.NetFramework:
                        return DbgEngineKind.Cordb;

                    default:
                        throw new UnknownEnumValueException(frameworkKind.Value);
                }
            }
            else
                return Engine;
        }

        private string GetModulePath(IDbgModule module)
        {
            var path = module.ToString();

            if (Host.GetPropertyValue<PSHost>("ExternalHost").UI is ConsoleChaosHostUserInterface)
            {
                //Managed assemblies loaded from the GAC will be very long, and we don't want to show a horizontal scrollbar when displaying modules
                //under a non-GUI console host. As such, if we see any modules in the GAC, shorten their names instead
                if (path.StartsWith("C:\\Windows\\Microsoft.NET\\assembly\\GAC", StringComparison.OrdinalIgnoreCase))
                    path = $"[GAC] {Path.GetFileName(path)}";
            }

            return path;
        }
    }
}
