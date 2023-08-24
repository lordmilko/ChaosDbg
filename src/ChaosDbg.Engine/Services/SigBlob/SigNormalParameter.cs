using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigNormalParameter : ISigParameter
    {
        GetParamPropsResult? Info { get; }
    }

    public class SigNormalParameter : ISigNormalParameter
    {
        public ISigType Type { get; }

        public GetParamPropsResult? Info { get; }

        internal SigNormalParameter(SigType type, GetParamPropsResult? info)
        {
            Type = type;
            Info = info;
        }

        public override string ToString()
        {
            if (Info == null)
                return Type.ToString();

            return $"{Type} {Info.Value.szName}";
        }
    }
}
