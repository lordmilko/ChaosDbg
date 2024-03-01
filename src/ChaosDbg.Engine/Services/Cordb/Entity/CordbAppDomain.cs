using System;
using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbAppDomain
    {
        private object assemblyLock = new object();

        #region ClrAddress

        private CLRDATA_ADDRESS? clrAddress;

        /// <summary>
        /// Gets the address of the clr!AppDomain that this object represents.
        /// </summary>
        public CLRDATA_ADDRESS ClrAddress
        {
            get
            {
                if (clrAddress == null)
                {
                    var sos = Process.DAC.SOS;

                    var appDomains = sos.GetAppDomainList(sos.AppDomainStoreData.DomainCount);

                    foreach (var appDomain in appDomains)
                    {
                        var data = sos.GetAppDomainData(appDomain);

                        if (data.dwId == CorDebugAppDomain.Id)
                        {
                            clrAddress = appDomain;
                            break;
                        }
                    }

                    if (clrAddress == null)
                        throw new InvalidOperationException($"Failed to get the address of AppDomain '{CorDebugAppDomain.Name}' (ID: {CorDebugAppDomain.Id})");
                }

                return clrAddress.Value;
            }
        }

        #endregion

        public CorDebugAppDomain CorDebugAppDomain { get; }

        public CordbProcess Process { get; }

        public IEnumerable<CordbAssembly> Assemblies
        {
            get
            {
                lock (assemblyLock)
                    return assemblies.ToList();
            }
        }

        private List<CordbAssembly> assemblies = new List<CordbAssembly>();

        public CordbAppDomain(CorDebugAppDomain corDebugAppDomain, CordbProcess process)
        {
            CorDebugAppDomain = corDebugAppDomain;
            Process = process;
        }

        internal void AddAssembly(CordbAssembly assembly)
        {
            lock (assemblyLock)
                assemblies.Add(assembly);

            if (assembly.AppDomain != null)
                throw new InvalidOperationException($"Cannot add assembly '{assembly}' to AppDomain '{this}': it is already a member of AppDomain '{assembly.AppDomain}'");

            assembly.AppDomain = this;
        }

        internal void RemoveAssembly(CordbAssembly assembly)
        {
            lock (assemblyLock)
                assemblies.Remove(assembly);

            assembly.AppDomain = null;
        }

        public override string ToString() => CorDebugAppDomain.Name;
    }
}
