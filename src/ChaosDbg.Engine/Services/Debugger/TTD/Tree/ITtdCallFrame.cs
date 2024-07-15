using System.Collections.Generic;

namespace ChaosDbg.TTD
{
    interface ITtdCallFrame
    {
        ITtdCallFrame Parent { get; set; }

        List<TtdCallFrame> Children { get; set; }

        List<TtdIndirectJumpEvent> Indirects { get; set; }
    }
}
