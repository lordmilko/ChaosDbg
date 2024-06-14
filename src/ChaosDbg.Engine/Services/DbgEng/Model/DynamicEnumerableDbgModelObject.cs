using System.Collections;
using System.Collections.Generic;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Encapsulates a DbgEng Data Model <see cref="ModelObject"/> that implements the <see cref="IterableConcept"/> as an <see cref="IEnumerable{T}"/> <see langword="dynamic"/>
    /// value that can easily be consumed from within managed code.
    /// </summary>
    class DynamicEnumerableDbgModelObject : DynamicDbgModelObject, IEnumerable<DynamicDbgModelObject>
    {
        public DynamicEnumerableDbgModelObject(ModelObject value, KeyStore metadata, DataModelManager dataModelManager) : base(value, metadata, dataModelManager)
        {
        }

        //Typically, callers will just "Foreach" over this, as a dynamic value, and the dynamic runtime infrastructure will automatically handle everything.
        //If you cast this object to an IEnumerable<dynamic>, this also "just works"
        public IEnumerator<DynamicDbgModelObject> GetEnumerator() => new Enumerator(Value, DataModelManager);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        struct Enumerator : IEnumerator<DynamicDbgModelObject>
        {
            public DynamicDbgModelObject Current { get; private set; }

            private DataModelManager dataModelManager;
            private ModelIterator iterator;
            private long dimensions;

            public Enumerator(ModelObject modelObject, DataModelManager dataModelManager)
            {
                this.dataModelManager = dataModelManager;
                var iterable = modelObject.Concept.Iterable.conceptInterface;
                dimensions = iterable.GetDefaultIndexDimensionality(modelObject.Raw);

                iterator = iterable.GetIterator(modelObject.Raw);

                Current = default;
            }

            public bool MoveNext()
            {
                var hr = iterator.TryGetNext(dimensions, out var result);

                if (hr == HRESULT.E_BOUNDS)
                {
                    Current = default;
                    return false;
                }

                hr.ThrowDbgEngNotOK();

                //Wrap the enumerated value up in an appropriate DynamicDbgModelObject sub-type for further interactions
                Current = DynamicDbgModelObject.New(result.@object, result.metadata, dataModelManager);
                return true;
            }

            object IEnumerator.Current => Current;

            public void Reset()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
