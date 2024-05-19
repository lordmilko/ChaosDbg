using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ChaosDbg.Terminal;
using ChaosLib;
using Microsoft.Win32;

namespace ChaosDbg.PowerShell.Host
{
    /// <summary>
    /// Represents a <see cref="ChaosHostUserInterface"/> that runs in a Windows Console.
    /// </summary>
    class ConsoleChaosHostUserInterface : ChaosHostUserInterface
    {
        public ConsoleChaosHostUserInterface(PSHostBase host, ITerminal terminal) : base(host, terminal)
        {
        }

        protected override void TryInstallPSReadLineShortcuts(Type psConsoleReadLineType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            var instanceField = psConsoleReadLineType.GetField("_singleton", flags);

            if (instanceField == null)
                goto incompatible;

            var setKeyHandlerMethodInfo = psConsoleReadLineType.GetMethod("SetKeyHandlerInternal", flags);
        
            if (setKeyHandlerMethodInfo == null)
                goto incompatible;

            var instance = instanceField.GetValue(null);

            void InstallKeyBinding(string key, string command)
            {
                setKeyHandlerMethodInfo.Invoke(
                    instance,
                    new object[]
                    {
                        new[]{key},
                        (Action<ConsoleKeyInfo?, object>) ((info, ctx) =>
                        {
                            /* The UI thread is currently blocked by PSConsoleReadLine.ReadLine(). We need to somehow signal to it that we want it to return. While you can pass a CancellationToken
                             * to PSReadLine, it only uses this token for cancelling the wait in older versions of PSReadLine (e.g. 2.0.0). Even if it did use it, it would also result in an
                             * OperationCanceledException which we don't want. Plan B might be to signal the _keyReadWaitHandle event so that ReadLine() wakes up, but for some reason I don't understand
                             * this may result in a spurious wake of _keyReadWaitHandle several reads down the track, resulting in a crash because its input queue will be empty. Thus, we'll go with Plan C:
                             * send a fake keyboard input to the console so that ReadLine() returns. */

                            commandOverride = command;

                            SendEnter();
                        }),
                        null, //briefDescription
                        null, //longDescription
                        null //scriptBlock
                    }
                );
            }

            InstallKeyBinding("F5", "g");
            InstallKeyBinding("F10", "p");
            InstallKeyBinding("F11", "t"); //Does not work in Windows Terminal unless you delete the toggle fullscreen action
            InstallKeyBinding("Shift+F11", "gu");

            if (IsRunningInWindowsTerminal() && WindowsTerminalF11IsBound())
                WriteWarningLine("ChaosDbg appears to be running inside Windows Terminal. F11 stepping may conflict with Windows Terminal's F11 'Toggle fullscreen' shortcut. Consider going to Settings -> Actions and removing the F11 'Toggle fullscreen' shortcut and then clicking Save. Fullscreen can still be toggled using Alt+Enter"); //temp

            return;

incompatible:
            WriteWarningLine("ChaosDbg is incompatible with the installed version of PSReadLine. Function key shortcuts will be unavailable");
        }

        private static void SendEnter()
        {
            var pushDown = new INPUT
            {
                Type = InputType.KEYBOARD,
                Input =
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (short) VirtualKey.Enter,
                        wScan = 0,
                        dwFlags = KEYEVENTF.NONE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
    }
}
            };

            var letUp = new INPUT
            {
                Type = InputType.KEYBOARD,
                Input =
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (short) VirtualKey.Enter,
                        wScan = 0,
                        dwFlags = KEYEVENTF.KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };

            var inputs = new[]
            {
                pushDown,
                letUp
            };
        private static bool IsRunningInWindowsTerminal()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
                return true;

            //WT_SESSION is not true if your default terminal is Windows Terminal and you simply double clicked chaos.exe

            var path = Registry.CurrentUser.OpenSubKey("Console\\%%Startup");

            if (path == null)
                return false;

            try
            {
                var value = new Guid(path.GetValue("DelegationConsole").ToString());

                //https://support.microsoft.com/en-us/windows/command-prompt-and-windows-powershell-for-windows-11-6453ce98-da91-476f-8651-5c14d5777c20

                if (value == new Guid("B23D10C0-E52E-411E-9D5B-C09FDF709C7D")) //Legacy
                    return false;

                return true;
            }
            finally
            {
                path.Dispose();
            }
        }

        private static bool WindowsTerminalF11IsBound()
        {
            /* If you add a reference to C:\Windows\System32\WinMetadata\Windows.Management.winmd you can
             * do new Windows.Management.Deployment.PackageManager() and the CLR will leverage its built-in WinRT
             * support to allow you to interact with the PackageManager type as if it's a CLR-compatible object.
             *
             * Under the hood, you can either call combase!RoGetActivationFactory or combase!RoActivateInstance to
             * get a Windows.Management.Deployment.PackageManager and cast it to an IPackageManager2 (see windows.management.deployment.idl)
             * The package manager is ultimately created by calling AppXDeploymentClient!DllGetActivationFactory and creating
             * the package manager instance from the factory.
             *
             * While I tested using this technique on Server 2016 without issue, I'm still a bit reluctant to add references
             * to WinRT craziness into ChaosDbg. You're supposed to add a reference to System.Runtime.WindowsRuntime, which I didn't do.
             * You _could_ just do a pure COM approach and define IPackageManager2 yourself...but at this stage I feel like it's
             * sufficient to just scan the filesystem, rather than load additional dependencies into our process. */

            var packages = Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "Packages");

            var settingsDir = Directory.EnumerateDirectories(packages, "Microsoft.WindowsTerminal_*").FirstOrDefault();

            if (settingsDir == null)
                return true;

            var settingsJson = Path.Combine(settingsDir, "LocalState", "settings.json");

            if (!File.Exists(settingsJson))
                return true;

            var str = File.ReadAllLines(settingsJson);

            for (var i = 0; i < str.Length; i++)
            {
                if (i < str.Length - 1 && str[i].Contains("\"command\": \"unbound\""))
                {
                    if (str[i + 1].Contains("\"keys\": \"f11\""))
                        return false;
                }
            }

            return true;
        }
    }
}
