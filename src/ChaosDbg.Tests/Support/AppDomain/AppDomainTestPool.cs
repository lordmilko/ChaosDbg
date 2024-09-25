using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChaosDbg.Tests
{
    class AppDomainTestPool
    {
        private static Stack<(AppDomain appDomain, AppDomainInvoker invoker)> appDomains = new();
        private static int nextId;
        private static object objLock = new object();

        public static (AppDomain appDomain, AppDomainInvoker invoker) Rent()
        {
            lock (objLock)
            {
                if (appDomains.Count > 0)
                {
                    return appDomains.Pop();
                }

                try
                {
                    var appDomain = AppDomain.CreateDomain($"ChaosDbg TestDomain {nextId++}", null, AppDomain.CurrentDomain.SetupInformation);
                    var invoker = (AppDomainInvoker) appDomain.CreateInstanceAndUnwrap(typeof(AppDomainTestPool).Assembly.FullName, typeof(AppDomainInvoker).FullName);

                    return (appDomain, invoker);
                }
                catch (Exception ex)
                {
                    Debug.Assert(false);

                    throw;
                }
            }
        }

        public static void Return((AppDomain appDomain, AppDomainInvoker invoker) item)
        {
            lock (objLock)
                appDomains.Push(item);
        }
    }
}
