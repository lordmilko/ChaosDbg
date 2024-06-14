using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChaosDbg
{
    //If our real store implementation implements two types of IEnumerable<T> it will mess up LINQ.
    //So use a wrapper type when exposing the store externally

    public interface IDbgEventFilterStore : IDbgEventFilterStoreInternal, IEnumerable<IDbgEventFilter>
    {
        new IEnumerator<IDbgEventFilter> GetEnumerator();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDbgEventFilterStoreInternal
    {
        IEnumerator<IDbgEventFilter> GetEnumerator();
    }

    /// <summary>
    /// Exposes an <see cref="IDbgEventFilterStoreInternal"/> as a type that implements <see cref="IEnumerable{IDbgEventFilter}"/>.
    /// </summary>
    class ExternalDbgEventFilterStore : IDbgEventFilterStore
    {
        private readonly IDbgEventFilterStoreInternal store;

        public ExternalDbgEventFilterStore(IDbgEventFilterStoreInternal store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            this.store = store;
        }

        public IEnumerator<IDbgEventFilter> GetEnumerator() => store.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
