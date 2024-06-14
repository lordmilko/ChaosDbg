using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a store used to manage and provide access to AppDomains that have been loaded into the current process.
    /// </summary>
    public class CordbAppDomainStore : IEnumerable<CordbAppDomain>
    {
        private object appDomainLock = new object();

        //As long as we hold a reference to an RCW, its reference count wont drop to 0, ensuring we're able to maintain
        //referential identity between the COM object in this dictionary and any new COM objects that come from ICorDebug
        private Dictionary<ICorDebugAppDomain, CordbAppDomain> appDomains = new Dictionary<ICorDebugAppDomain, CordbAppDomain>();

        private CordbProcess process;

        public CordbAppDomainStore(CordbProcess process)
        {
            this.process = process;
        }

        public void Add(CorDebugAppDomain corDebugAppDomain)
        {
            var appDomain = new CordbAppDomain(corDebugAppDomain, process);

            //Use Add() so that we assert if we try and add the same AppDomain twice
            lock (appDomainLock)
                appDomains.Add(corDebugAppDomain.Raw, appDomain);
        }

        public CordbAppDomain Remove(CorDebugAppDomain corDebugAppDomain)
        {
            lock (appDomainLock)
            {
                if (appDomains.TryGetValue(corDebugAppDomain.Raw, out var appDomain))
                {
                    appDomains.Remove(corDebugAppDomain.Raw);

                    //Destroy the relationship between this AppDomain and any remaining assemblies. I don't know if we would have
                    //received an UnloadAssembly notification for each assembly prior to receiving an ExitAppDomain event
                    foreach (var assembly in appDomain.Assemblies)
                        assembly.AppDomain = null;
                }

                return appDomain;
            }
        }

        /// <summary>
        /// Associates a given <see cref="CordbAssembly"/> with a <see cref="CordbAppDomain"/> via its parent <see cref="CorDebugAppDomain"/>.
        /// </summary>
        /// <param name="assembly">The assembly that should be associated.</param>
        public void LinkAssembly(CordbAssembly assembly)
        {
            var appDomain = GetAppDomain(assembly.CorDebugAssembly.AppDomain);

            appDomain.AddAssembly(assembly);
        }

        /// <summary>
        /// Disassociates a given <see cref="CordbAssembly"/> with its parent <see cref="CordbAppDomain"/>.
        /// </summary>
        /// <param name="assembly">The assembly that should be dissociated.</param>
        public void UnlinkAssembly(CordbAssembly assembly) =>
            assembly.AppDomain?.RemoveAssembly(assembly);

        /// <summary>
        /// Gets the <see cref="CordbAppDomain"/> that wraps a given <see cref="CorDebugAppDomain"/>.
        /// </summary>
        /// <param name="corDebugAppDomain">The underlying <see cref="CorDebugAppDomain"/> to lookup.</param>
        /// <returns>The <see cref="CordbAppDomain"/> that wraps the given <see cref="CorDebugAppDomain"/>.</returns>
        internal CordbAppDomain GetAppDomain(CorDebugAppDomain corDebugAppDomain)
        {
            lock (appDomainLock)
            {
                if (!appDomains.TryGetValue(corDebugAppDomain.Raw, out var appDomain))
                    throw new InvalidOperationException($"Could not find the existing AppDomain that COM object '{corDebugAppDomain}' corresponds to.");

                return appDomain;
            }
        }

        public IEnumerator<CordbAppDomain> GetEnumerator()
        {
            lock (appDomainLock)
                return appDomains.Values.ToList().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
