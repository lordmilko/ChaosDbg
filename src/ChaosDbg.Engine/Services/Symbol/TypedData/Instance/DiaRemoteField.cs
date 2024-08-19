using System;
using ChaosLib.TypedData;

namespace ChaosDbg.TypedData
{
    class DiaRemoteField : IDbgRemoteField
    {
        public long Address => throw new NotImplementedException();
        public string Name => throw new NotImplementedException();
    }
}
