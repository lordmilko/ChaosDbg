﻿using System;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace ChaosDbg.MSBuild
{
    public class GenerateViewModels : Task
    {
        [Required]
        public string[] Files { get; set; }

        [Required]
        public string Output { get; set; }

        public override bool Execute()
        {
            var appDomain = AppDomain.CreateDomain("ChaosDbgAppDomain");

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

                var errors = helper.ExecuteRemote(Files, Output);

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

            return true;
        }
    }
}
