using System;
using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbAssembly
    {
        #region ClrAddress

        private CLRDATA_ADDRESS? clrAddress;

        /// <summary>
        /// Gets the address of the clr!Assembly that this object represents.
        /// </summary>
        public CLRDATA_ADDRESS ClrAddress
        {
            get
            {
                if (clrAddress == null)
                {
                    var sos = Process.DAC.SOS;

                    var appDomainAddr = AppDomain.ClrAddress;

                    var assemblyList = sos.GetAssemblyList(appDomainAddr);

                    foreach (var assembly in assemblyList)
                    {
                        var location = sos.GetAssemblyLocation(assembly);
                        var name = sos.GetAssemblyName(assembly);

                        Debug.Assert(location == name);

                        if (StringComparer.OrdinalIgnoreCase.Equals(location, CorDebugAssembly.Name))
                        {
                            clrAddress = assembly;
                            break;
                        }
                    }

                    if (clrAddress == null)
                        throw new InvalidOperationException($"Failed to get the address of assembly '{CorDebugAssembly}'");
                }

                return clrAddress.Value;
            }
        }

        #endregion

        public CordbAppDomain AppDomain { get; internal set; }

        public CordbManagedModule Module { get; internal set; }

        public CorDebugAssembly CorDebugAssembly { get; }

        public CordbProcess Process { get; }

        public CordbAssembly(CorDebugAssembly corDebugAssembly, CordbProcess process)
        {
            CorDebugAssembly = corDebugAssembly;
            Process = process;
        }

        public override string ToString()
        {
            return CorDebugAssembly.Name;
        }
    }
}
