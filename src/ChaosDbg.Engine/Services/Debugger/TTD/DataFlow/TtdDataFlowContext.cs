using System.Collections.Generic;
using ChaosDbg.Disasm;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    /// <summary>
    /// Stores information that is passed to a variety of methods that assist with performing TTD Data Flow.
    /// </summary>
    class TtdDataFlowContext
    {
        public Cursor Cursor { get; }

        public TtdSymbolManager SymbolManager { get; }

        public INativeDisassembler Disassembler { get; }

        public Dictionary<TtdDataFlowItem, CrossPlatformContext> ParentToRegisterContext { get; } = new Dictionary<TtdDataFlowItem, CrossPlatformContext>();

        public long TargetValue { get; }

        public int ValueSize { get; }

        public TtdDataFlowContext(Cursor cursor, TtdSymbolManager symbolManager, INativeDisassembler disassembler, long targetValue, int valueSize)
        {
            Cursor = cursor;
            SymbolManager = symbolManager;
            Disassembler = disassembler;
            TargetValue = targetValue;
            ValueSize = valueSize == 0 ? 1 : valueSize;
        }
    }
}
