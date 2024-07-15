using System.CommandLine;
using ChaosDbg;
using ChaosDbg.Metadata;

namespace chaos
{
    class Program
    {
        static void Main(string[] args)
        {
            //In single file builds, a zip file is embedded in the exe containing all managed/unmanaged assemblies,
            //making ChaosDbg easily portable as well as reducing the overall file size
            SingleFileProvider.ExtractChaosDbg();

            Run(args);
        }

        private static bool HasMinimumFrameworkVersion()
        {
            /* ChaosDbg requires at least 4.7.1 to run. This is due to the fact that it references libraries that target .NET Standard 2.0 (e.g. ClrDebug).
             * The "normal" way that you enforce a given minimum version is by specifying a "supportedRuntime" field in your app.config file, which is then
             * parsed by ICLRMetaHostPolicy. As we want to publish as a single file, this will not work for us. We could potentially call ICLRMetaHostPolicy::GetRequestRuntime
             * and pass in an app.config ourselves (with the appropriate METAHOST_POLICY_FLAGS to show a dialog on error), however this way of detecting the installed
             * version of the CLR isn't even any good anyway:
             * - First of all, mscoreei!RuntimeRequest::ComputeVersionString calls mscoreei!RuntimeRequest::ProcessRuntimeNotFoundAction which then calls back into
             *   mscoreei!RuntimeRequest::ComputeVersionString again, bringing us to another call to ProcessRuntimeNotFoundAction, resulting in
             *   mscoreei!RuntimeRequest::ShowErrorDialog being called twice as each ProcessRuntimeNotFoundAction() function exits
             * - The prompt that you get telling you to install the newer version of .NET brings up a useless Bing page which didn't even do the search
             * - The way that the detection seems to work is that it queries the SKUs listed under HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\.NETFramework\v4.0.30319\SKUs
             *   The issue with this is that I don't know 100% whether every previous SKU version is guaranteed to be listed here (it does look like that is true, but
             *   I'm still not 100% confident that we should rely on this). Another recommended approach is to look for a "feature" (maybe an API) that was only introduced
             *   a given version, but I'm not sure what API we would use for this
             *
             * So, we'll fallback to the old reliable strategy of checking the .NET Framework release version. If, for some inexplicable reason we don't have the required registry
             * value, we'll give the user the benefit of the doubt and see how we go */

            var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\NET Framework Setup\\NDP\\v4\\Full");

            if (key == null)
                return true; //Give the user the benefit of the doubt

            var release = key.GetValue("Release");

            if (release == null)
                return true; //Give the user the benefit of the doubt

            const int net472 = 461808;

            if ((int) release < net472)
            {
                var result = User32.Native.MessageBoxW(
                    IntPtr.Zero,
                    "This application requires one of the following versions of the .NET Framework:\n .NETFramework,Version=v4.7.2\n\nDo you want to install this .NET Framework version now?",
                    $"{Process.GetCurrentProcess().ProcessName}.exe - This application could not be started.",
                    MB.MB_ICONERROR | MB.MB_YESNO
                );

                //I couldn't find a permalink to use, so this will have to do
                if (result == ID.IDYES)
                    Process.Start("https://dotnet.microsoft.com/en-us/download/dotnet-framework");

                return false;
            }

            return true;
        }

        private static void Run(string[] args)
        {
            var executableArgument = new Argument<string>("executable", "The command line of an executable process to launch and debug");

            var engineOption = new Option<DbgEngineKind?>(new[]{"-e", "--engine"}, "The debug engine to use");
            var minimizedOption = new Option<bool>("--minimized", "Whether to start the process minimized");
            var frameworkKindOption = new Option<FrameworkKind?>(new[]{"-k", "--frameworkKind" }, "The kind of .NET runtime this EXE launches (if applicable)");

            var root = new RootCommand("ChaosDbg CLI")
            {
                executableArgument,
                engineOption,
                minimizedOption,
                frameworkKindOption
            };
            root.SetHandler(
                (executable, engineKind, minimized, frameworkKind) =>
                {
                    var kind = GetEngineKind(executable, engineKind);

                    switch (kind)
                    {
                        case DbgEngineKind.DbgEng:
                            GlobalProvider.ServiceProvider.GetService<DbgEngClient>().Execute(executable, minimized);
                            break;

                        case DbgEngineKind.Cordb:
                        case DbgEngineKind.Interop:
                            GlobalProvider.ServiceProvider.GetService<CordbClient>().Execute(executable, minimized, kind == DbgEngineKind.Interop, frameworkKind);
                            break;

                        default:
                            throw new UnknownEnumValueException(kind);
                    }
                },
                executableArgument, engineOption, minimizedOption, frameworkKindOption
            );

            root.Invoke(args);
        }

        private static DbgEngineKind GetEngineKind(string executable, DbgEngineKind? engineKind)
        {
            if (engineKind == null)
            {
                var detector = GlobalProvider.ServiceProvider.GetService<IFrameworkTypeDetector>();

                var type = detector.Detect(executable);

                switch (type)
                {
                    case FrameworkKind.Native:
                        return DbgEngineKind.DbgEng;

                    case FrameworkKind.NetCore:
                    case FrameworkKind.NetFramework:
                        return DbgEngineKind.Cordb;

                    default:
                        throw new UnknownEnumValueException(type);
                }
            }
            else
                return engineKind.Value;
        }
    }
}
