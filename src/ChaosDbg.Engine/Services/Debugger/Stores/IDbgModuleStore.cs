using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;

namespace ChaosDbg
{
    //If our real store implementation implements two types of IEnumerable<T> it will mess up LINQ.
    //So use a wrapper type when exposing the store externally

    public interface IDbgModuleStore : IEnumerable<IDbgModule>
    {
        new IEnumerator<IDbgModule> GetEnumerator();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDbgModuleStoreInternal
    {
        IEnumerator<IDbgModule> GetEnumerator();
    }

    /// <summary>
    /// Exposes an <see cref="IDbgEventFilterStoreInternal"/> as a type that implements <see cref="IEnumerable{IDbgModule}"/>.
    /// </summary>
    class ExternalDbgModuleStore : IDbgModuleStore
    {
        private readonly IDbgModuleStoreInternal store;

        public ExternalDbgModuleStore(IDbgModuleStoreInternal store)
        {
            if (store == null)
                throw new ArgumentNullException(nameof(store));

            this.store = store;
        }

        public IEnumerator<IDbgModule> GetEnumerator() => store.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
