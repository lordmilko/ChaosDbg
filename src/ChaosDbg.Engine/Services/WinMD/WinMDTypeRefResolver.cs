using System.Collections.Generic;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WinMDTypeRefResolver : TypeRefResolver
    {
        public HashSet<mdTypeRef> ToRemove { get; } = new HashSet<mdTypeRef>();

        protected override bool ShouldResolveAssemblyRef(mdTypeRef typeRef, string assemblyRef, string typeName)
        {
            switch (assemblyRef)
            {
                //Probably something WinRT related. We have all typedef props already. Dont care.
                //In the future we could maybe allow pointing to a folder containing additional
                //*.winmd files to digest, however this will require having a separate typeRefCache
                //per file
                case "Windows.Foundation.FoundationContract":
                case "Windows.Foundation.UniversalApiContract":
                    return false;

                case "netstandard":
                    switch (typeName)
                    {
                        case "System.Attribute":
                            ToRemove.Add(typeRef);
                            return false;
                    }
                    break;
            }

            return true;
        }
    }
}
