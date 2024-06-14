using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using ChaosDbg.Terminal;
using ChaosLib;

namespace ChaosDbg.PowerShell.Host
{
    enum PSReadLineType
    {
        v2_0,
        v2_4
    }

    /// <summary>
    /// Represents a <see cref="PSHostUserInterface"/> with ChaosDbg specific customizations
    /// </summary>
    abstract class ChaosHostUserInterface : PSHostUserInterfaceBase
    {
        private bool hasTriedRawPSReadLine;
        private MethodInfo rawPSReadLine;
        private PrivateField<int> initialY;
        private PSReadLineType psReadLineType;
        protected string commandOverride;

        protected ChaosHostUserInterface(PSHostBase host, ITerminal terminal) : base(host, terminal)
        {
        }

        protected override bool TryInvokePSReadLineRaw(CommandInfo psReadLineCommand, EngineIntrinsics engineIntrinsics, out string input)
        {
            //This doesn't really seem to be much faster than invoking PSReadLine via PowerShell normally, but I'm sure it is in fact faster

            input = default;

            if (rawPSReadLine == null && hasTriedRawPSReadLine)
                return false;

            hasTriedRawPSReadLine = true;

            if (psReadLineCommand is FunctionInfo f && f.ModuleName == "PSReadLine")
            {
                var assembly = f.Module.NestedModules.FirstOrDefault()?.ImplementingAssembly;

                if (!TryGetPSReadLineMembers(assembly))
                    return false;
            }
            else
                return false;

            //Invoking PSReadLine directly requires that the default runspace has been set, as the PSConsoleReadLine cctor will try and do PowerShell.Create for the "current runspace"

            object[] psReadLineArgs;

            switch (psReadLineType)
            {
                case PSReadLineType.v2_0:
                    psReadLineArgs = new object[] { host.Runspace, engineIntrinsics };
                    break;

                case PSReadLineType.v2_4:
                    psReadLineArgs = new object[] { host.Runspace, engineIntrinsics, null };
                    break;

                default:
                    throw new UnknownEnumValueException(psReadLineType);
            }

            input = (string) rawPSReadLine.Invoke(null, psReadLineArgs);

            //WinDbg does not retain the previous input you were typing when you hit F11, so we maintain the same behavior
            if (commandOverride != null)
            {
                WriteLine();
                input = commandOverride;
                commandOverride = null;
            }

            return true;
        }

        private bool TryGetPSReadLineMembers(Assembly assembly)
        {
            var psConsoleReadLineType = assembly.GetType("Microsoft.PowerShell.PSConsoleReadLine");

            if (psConsoleReadLineType == null)
                return false;

            //In PSReadline 2.0.0 there are just two parameters, but in PSReadLine 2.4.0 there are three

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            rawPSReadLine = psConsoleReadLineType.GetMethod("ReadLine", flags, null, new[] { typeof(Runspace), typeof(EngineIntrinsics) }, null);

            if (rawPSReadLine == null)
            {
                rawPSReadLine = psConsoleReadLineType.GetMethod("ReadLine", flags, null, new[] { typeof(Runspace), typeof(EngineIntrinsics), typeof(bool?) }, null);

                if (rawPSReadLine == null)
                    return false;

                psReadLineType = PSReadLineType.v2_4;
            }
            else
                psReadLineType = PSReadLineType.v2_0;

            var instanceField = psConsoleReadLineType.GetFieldInfo("_singleton");

            var instance = instanceField.GetValue(null);

            initialY = new PrivateField<int>(instance, "_initialY");

            terminal.OnProtectedWrite = inc =>
            {
                initialY.Value += inc;
            };

            //ReadLine() is the only mandatory member; if we can set key handlers too, that's an added bonus
            TryInstallPSReadLineShortcuts(instance);
            return true;
        }

        protected abstract void TryInstallPSReadLineShortcuts(object psConsoleReadLineInstance);
    }
}
