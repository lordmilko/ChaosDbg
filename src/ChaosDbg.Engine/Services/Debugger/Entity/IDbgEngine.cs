using System;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;

namespace ChaosDbg
{
    /// <summary>
    /// Represents a debugger engine.<para/>
    /// Concrete implementations include <see cref="CordbEngine"/> and <see cref="DbgEngEngine"/>.
    /// </summary>
    public interface IDbgEngine : IDisposable
    {
        IDbgProcess ActiveProcess { get; }

        IDbgSessionInfo Session { get; }
    }
}
