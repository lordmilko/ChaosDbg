using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChaosDbg
{
    //If our real store implementation implements two types of IEnumerable<T> it will mess up LINQ.
    //So use a wrapper type when exposing the store externally

    public interface IDbgThreadStore : IDbgThreadStoreInternal, IEnumerable<IDbgThread>
    {
        new IEnumerator<IDbgThread> GetEnumerator();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDbgThreadStoreInternal
    {
        IDbgThread ActiveThread { get; }

        IEnumerator<IDbgThread> GetEnumerator();
    }

    /// <summary>
    /// Exposes an <see cref="IDbgThreadStoreInternal"/> as a type that implements <see cref="IEnumerable{IDbgThread}"/>.
    /// </summary>
    class ExternalDbgThreadStore : IDbgThreadStore
    {
        private readonly IDbgThreadStoreInternal store;

        public ExternalDbgThreadStore(IDbgThreadStoreInternal store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            this.store = store;
        }

        public IDbgThread ActiveThread => store.ActiveThread;

        public IEnumerator<IDbgThread> GetEnumerator() => store.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
