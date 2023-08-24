using System.Collections.Generic;
using ClrDebug;

namespace ChaosDbg
{
    public class ModuleResolutionContext
    {
        public MetaDataImport Import { get; }

        public Dictionary<mdTypeRef, ResolvedTypeRef> ResolvedTypeRefs { get; } = new Dictionary<mdTypeRef, ResolvedTypeRef>();

        public Dictionary<mdMemberRef, ResolvedMemberRef> ResolvedMemberRefs { get; } = new Dictionary<mdMemberRef, ResolvedMemberRef>();

        public ModuleResolutionContext(MetaDataImport import)
        {
            Import = import;
        }
    }
}
