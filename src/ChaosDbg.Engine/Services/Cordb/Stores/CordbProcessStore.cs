using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a store used to manage and provide access to the processes that are being debugged by a given <see cref="CordbEngine"/>.
    /// ChaosDbg only supports a single <see cref="CordbProcess"/> per <see cref="CordbEngine"/>; as such, this object always contains exactly
    /// one <see cref="CordbProcess"/>.
    /// </summary>
    public class CordbProcessStore : IDbgProcessStoreInternal, IEnumerable<CordbProcess>
    {
        public CordbProcess ActiveProcess { get; set; }

        internal CordbProcessStore(CordbProcess process)
        {
            ActiveProcess = process;
        }

        public void Remove(CordbProcess process)
        {
            Debug.Assert(ActiveProcess == process || process == null); ;
            ActiveProcess = null;
        }

        #region IDbgProcessStore

        IDbgProcess IDbgProcessStoreInternal.ActiveProcess => ActiveProcess;

        #endregion

        public IEnumerator<CordbProcess> GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        IEnumerator<IDbgProcess> IDbgProcessStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
