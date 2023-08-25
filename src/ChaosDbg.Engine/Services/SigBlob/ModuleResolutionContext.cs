using System.Collections.Generic;
using System.Linq;
using ClrDebug;

namespace ChaosDbg
{
    public class ModuleResolutionContext
    {
        public MetaDataImport Import { get; }

        //In *.winmd files, you can have multiple types with the exact same name and namespace (for the purposes of having architecture specific implementations of a given type)
        Dictionary<string, mdTypeDef[]> nameToTypeDefCache;

        public Dictionary<string, mdTypeDef[]> NameToTypeDefCache
        {
            get
            {
                if (nameToTypeDefCache == null)
                {
                    var types = Import.EnumTypeDefs();
                    nameToTypeDefCache = types.Select(t => new
                    {
                        Token = t,
                        Name = Import.GetTypeDefProps(t).szTypeDef
                    }).GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.Select(v => v.Token).ToArray());
                }

                return nameToTypeDefCache;
            }
        }

        public Dictionary<mdTypeRef, IResolvedTypeRef> ResolvedTypeRefs { get; } = new Dictionary<mdTypeRef, IResolvedTypeRef>();

        public Dictionary<mdMemberRef, ResolvedMemberRef> ResolvedMemberRefs { get; } = new Dictionary<mdMemberRef, ResolvedMemberRef>();

        public ModuleResolutionContext(MetaDataImport import)
        {
            Import = import;
        }
    }
}
