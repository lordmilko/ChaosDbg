using System.Collections.Generic;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    class TtdRootFrame : ITtdCallFrame
    {
        public TtdRawFunctionCall Function { get; set; }

        public ThreadInfo Thread { get; set; }

        public ITtdCallFrame Parent { get; set; }
        public List<TtdCallFrame> Children { get; set; } = new List<TtdCallFrame>();
        public List<TtdIndirectJumpEvent> Indirects { get; set; }
    }
}
