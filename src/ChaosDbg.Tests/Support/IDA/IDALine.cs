using System.Globalization;
using ChaosDbg.Analysis;
using ClrDebug;

namespace ChaosDbg.Tests
{
    class IDALine
    {
        public string Section { get; }

        public CORDB_ADDRESS VirtualAddress { get; }

        public CORDB_ADDRESS PhysicalAddress { get; }

        public string Content { get; }

        public IDALineKind Kind { get; }

        public bool IsIdentifier
        {
            get
            {
                if (Content.EndsWith(":") && !Content.StartsWith(":"))
                {
                    //Ensure it's not an ANSI string

                    if (Content.Length > 2 && !(Content[0] == 'a' && char.IsUpper(Content[1])))
                    {
                        //Usually something to do with function unwinding that has a symbol associated with it
                        return true;
                    }
                }

                return false;
            }
        }

        public IDALine(string sectionAndAddr, string content, IDALineKind kind, PEMetadataPhysicalModule module)
        {
            var split = sectionAndAddr.Split(':');
            Section = split[0];
            PhysicalAddress = long.Parse(split[1], NumberStyles.HexNumber);

            if (module is PEMetadataVirtualModule v)
                VirtualAddress = v.GetVirtualAddress(PhysicalAddress);
            else
                VirtualAddress = PhysicalAddress;

            Content = content;
            Kind = kind;
        }

        public override string ToString()
        {
            return $"[{Kind}] {Content}";
        }
    }
}
