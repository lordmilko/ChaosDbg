using ClrDebug;

namespace ChaosDbg
{
    public class ResolvedTypeRef
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
