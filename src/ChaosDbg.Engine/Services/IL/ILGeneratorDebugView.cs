using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using ChaosLib;

namespace ChaosDbg.IL
{
    class ILGeneratorDebugView
    {
        private ILGenerator ilg;

        public ILGeneratorDebugView(ILGenerator ilg)
        {
            this.ilg = ilg;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ILInstruction[] Instructions
        {
            get
            {
                //You can have DynamicILGenerator, which means we won't get the fields from the base class

                var streamFieldInfo = typeof(ILGenerator).GetFieldInfo("m_ILStream");
                var lengthFieldInfo = typeof(ILGenerator).GetFieldInfo("m_length");

                var stream = (byte[]) streamFieldInfo.GetValue(ilg);
                var length = (int) lengthFieldInfo.GetValue(ilg);

                Array.Resize(ref stream, length);

                var instrs = ILDisassembler.Create(stream).EnumerateInstructions().ToArray();

                return instrs;
            }
        }
    }
}
