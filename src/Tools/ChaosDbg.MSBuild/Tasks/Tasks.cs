using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
#if NET
using System.Runtime.Loader;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace ChaosDbg.MSBuild
{
    public class GenerateViewModels : GeneratorTask
    {
    }

    public class GenerateDependencyProperties : GeneratorTask
    {
    }

    public class GeneratorTask : Task
    {
        [Required]
        public string[] Files { get; set; }

        [Required]
        public string Output { get; set; }

        public override bool Execute()
        {
            var name = $"ChaosDbgAppDomain_{GetType().Name}";

#if NET
            var loadContext = new AssemblyLoadContext(name, true);

            try
            {
                var assembly = loadContext.LoadFromAssemblyPath(GetType().Assembly.Location);

                var remoteGeneratorType = assembly.GetType(typeof(RemoteGenerator).FullName, true);

                var helper = Activator.CreateInstance(remoteGeneratorType);

                var executeRemote = helper.GetType().GetMethod(nameof(RemoteGenerator.ExecuteRemote));

                if (executeRemote == null)
                    throw new MissingMethodException(typeof(RemoteGenerator).FullName, nameof(RemoteGenerator.ExecuteRemote));

                try
                {
                    var errors = (string[]) executeRemote.Invoke(helper, new object[] { Files, Output, GetType().Name });

                    if (errors.Length > 0)
                    {
                        foreach (var item in errors)
                            Log.LogError(item);

                        return false;
                    }
                }
                catch (TargetInvocationException ex)
                {
                    ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                }
            }
            finally
            {
                loadContext.Unload();
            }
#else
            var appDomain = AppDomain.CreateDomain(name);

            try
            {
                var helper = (RemoteGenerator) appDomain.CreateInstanceFromAndUnwrap(
                    GetType().Assembly.Location,
                    typeof(RemoteGenerator).FullName,
                    false,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new object[0],
                    null,
                    null
                );

                var errors = helper.ExecuteRemote(Files, Output, GetType().Name);

                if (errors.Length > 0)
                {
                    foreach (var item in errors)
                        Log.LogError(item);

                    return false;
                }
            }
            finally
            {
                AppDomain.Unload(appDomain);
            }
#endif

            return true;
        }
    }
}
