using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChaosDbg
{
    //If our real store implementation implements two types of IEnumerable<T> it will mess up LINQ.
    //So use a wrapper type when exposing the store externally

    /// <summary>
    /// Represents a store used to manage and provide access to the processes that are being debugged by a given <see cref="IDbgEngine"/>.<para/>
    /// Concrete implementations include <see cref="CordbProcessStore"/> and <see cref="DbgEngProcessStore"/>.
    /// </summary>
    public interface IDbgProcessStore : IDbgProcessStoreInternal, IEnumerable<IDbgProcess>
    {
        new IEnumerator<IDbgProcess> GetEnumerator();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDbgProcessStoreInternal
    {
        IDbgProcess ActiveProcess { get; }

        IEnumerator<IDbgProcess> GetEnumerator();
    }

    /// <summary>
    /// Exposes an <see cref="ChaosDbg.IDbgEventFilterStoreInternal"/> as a type that implements <see cref="IEnumerable{IDbgProcess}"/>.
    /// </summary>
    class ExternalDbgProcessStore : IDbgProcessStore
    {
        private readonly IDbgProcessStoreInternal store;

        public IDbgProcess ActiveProcess => store.ActiveProcess;

        public ExternalDbgProcessStore(IDbgProcessStoreInternal store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            this.store = store;
        }

        public IEnumerator<IDbgProcess> GetEnumerator() => store.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
