using ClrDebug;

namespace ChaosDbg
{
    public interface IResolvedTypeRef
    {
        mdTypeRef TypeRef { get; }

        ModuleResolutionContext TypeRefModule { get; }

        ModuleResolutionContext TypeDefModule { get; }
    }

    public class AmbiguousResolvedTypeRef : IResolvedTypeRef
    {
        public mdTypeRef TypeRef { get; }

        public mdTypeDef[] TypeDefs { get; }

        public ModuleResolutionContext TypeRefModule { get; }

        public ModuleResolutionContext TypeDefModule { get; }

        public AmbiguousResolvedTypeRef(mdTypeRef typeRef, mdTypeDef[] typeDefs, ModuleResolutionContext typeRefModule, ModuleResolutionContext typeDefModule)
        {
            TypeRef = typeRef;
            TypeDefs = typeDefs;
            TypeRefModule = typeRefModule;
            TypeDefModule = typeDefModule;
        }
    }

    public class ResolvedTypeRef : IResolvedTypeRef
    {
        public mdTypeRef TypeRef { get; }

        public mdTypeDef TypeDef { get; }

        public ModuleResolutionContext TypeRefModule { get; }

        public ModuleResolutionContext TypeDefModule { get; }

        public ResolvedTypeRef(mdTypeRef typeRef, mdTypeDef typeDef, ModuleResolutionContext typeRefModule, ModuleResolutionContext typeDefModule)
        {
            TypeRef = typeRef;
            TypeDef = typeDef;
            TypeRefModule = typeRefModule;
            TypeDefModule = typeDefModule;
        }
    }
}
