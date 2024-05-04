using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using ChaosLib.TypedData;

namespace ChaosDbg.TypedData
{
    class DiaRemoteFieldCollection : IDbgRemoteFieldCollection
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private DiaRemoteType type;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected ITypedDataProvider provider;

        public DiaRemoteFieldCollection(DiaRemoteType type, ITypedDataProvider provider)
        {
            this.type = type;
            this.provider = provider;
        }

        public IEnumerator<IDbgRemoteField> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public IDbgRemoteField this[string name] => throw new NotImplementedException();
    }
}
