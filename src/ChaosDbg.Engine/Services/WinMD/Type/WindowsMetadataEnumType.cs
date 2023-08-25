using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataEnumType : WindowsMetadataType
    {
        public WindowsMetadataEnumType(mdTypeDef typeDef, GetTypeDefPropsResult props) : base(typeDef, props)
        {
        }
    }
}
