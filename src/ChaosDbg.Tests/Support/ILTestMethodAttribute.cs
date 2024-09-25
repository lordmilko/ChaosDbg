using ChaosDbg.IL;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    class ILTestMethodAttribute : TestMethodAttribute
    {
        public OpCodeKind[] Kind { get; }

        public ILTestMethodAttribute(params OpCodeKind[] kind)
        {
            Kind = kind;
        }
    }
}
