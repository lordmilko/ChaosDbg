using System;

namespace ChaosDbg.PowerShell.Host
{
    /// <summary>
    /// Provides a mechanism for callers to start the ChaosDbg PowerShell Host without referencing System.Management.Automation.
    /// </summary>
    public static class ChaosShell
    {
        public static int Start(IServiceProvider serviceProvider, params string[] args)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var cpp = new CommandLineParameterParser();
            cpp.Parse(args);

            var host = new ChaosDbgHost(serviceProvider, cpp);

            cpp.ShowErrorHelpBanner(host.UI, null, null);

            return host.Run();
        }
    }
}
