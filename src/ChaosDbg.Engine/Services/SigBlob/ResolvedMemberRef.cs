using ClrDebug;

namespace ChaosDbg
{
    public class ResolvedMemberRef
    {
        public mdMemberRef MemberRef { get; }

        public mdToken Token { get; }

        public ModuleResolutionContext MemberRefModule { get; }

        public ModuleResolutionContext TokenModule { get; }

        public ResolvedMemberRef(mdMemberRef memberRef, mdToken token, ModuleResolutionContext memberRefModule, ModuleResolutionContext tokenModule)
        {
            MemberRef = memberRef;
            Token = token;
            MemberRefModule = memberRefModule;
            TokenModule = tokenModule;
        }
    }
}
