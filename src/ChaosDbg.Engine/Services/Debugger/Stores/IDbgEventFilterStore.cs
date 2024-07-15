using System.Collections.Generic;
using ChaosDbg.Debugger;

namespace ChaosDbg
{
    public interface IDbgEventFilterStore : IEnumerable<DbgEventFilter>
    {
        void SetArgument(WellKnownEventFilter kind, string argumentValue);

        DbgEventFilter this[WellKnownEventFilter kind] { get; }
    }
}
