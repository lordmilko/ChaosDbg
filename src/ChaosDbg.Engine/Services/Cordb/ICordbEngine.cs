using System;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public interface ICordbEngine : IDbgEngine, IDisposable
    {
        #region Control
        
        void Continue();

        void Break();

        #endregion
    }
}
