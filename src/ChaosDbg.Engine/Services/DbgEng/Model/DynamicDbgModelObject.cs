using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using ChaosLib.Dynamic;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng.Model
{
    /// <summary>
    /// Encapslates a DbgEng Data Model <see cref="ModelObject"/> as a <see langword="dynamic"/> value that can easily be consumed from within managed code.
    /// </summary>
    internal class DynamicDbgModelObject : IDynamicMetaObjectProvider
    {
        /// <summary>
        /// Gets the underlying <see cref="ModelObject"/> encapsulated by this proxy.
        /// </summary>
        internal ModelObject Value { get; }

        /// <summary>
        /// Gets the <see cref="DataModelManager"/> that should be used to wrap CLR values as <see cref="ModelObject"/> intrinsics in order to be passed as arguments to functions/indexers we try to invoke on our <see cref="Value"/>.
        /// </summary>
        internal DataModelManager DataModelManager { get; }

        /// <summary>
        /// Gets the Help string that is defined for this object, or <see langword="null"/> if no help is defined.
        /// </summary>
        internal string Help
        {
            get
            {
                if (metadata == null)
                    return null;

                if (metadata.TryGetKeyValue(WellKnownModelMetadata.Help, out var help) == HRESULT.S_OK)
                    return help.@object.IntrinsicValue?.ToString();

                return null;
            }
        }

        private KeyStore metadata;

        /// <summary>
        /// Creates a specific kind of <see cref="DynamicDbgModelObject"/> that may implement additional CLR interfaces based on the concepts supported by a given <see cref="ModelObject"/>.
        /// </summary>
        /// <param name="value">The <see cref="ModelObject"/> to encapsulate.</param>
        /// <param name="metadata">Any metadata that may be associated with the given <paramref name="metadata"/> value.</param>
        /// <param name="dataModelManager">The <see cref="DataModelManager"/> that was used to create the <see cref="ModelObject"/>.</param>
        /// <returns>A <see cref="DynamicDbgModelObject"/> that encapsulates the specified value.</returns>
        public static DynamicDbgModelObject New(ModelObject value, KeyStore metadata, DataModelManager dataModelManager)
        {
            var iterable = value.Concept.Iterable;

            if (iterable != null)
                return new DynamicEnumerableDbgModelObject(value, metadata, dataModelManager);

            return new DynamicDbgModelObject(value, metadata, dataModelManager);
        }

        protected DynamicDbgModelObject(ModelObject value, KeyStore metadata, DataModelManager dataModelManager)
        {
            this.metadata = metadata;

            Value = value;
            DataModelManager = dataModelManager;
        }

        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new DynamicMetaObject<DynamicDbgModelObject>(parameter, this, new DbgModelObjectMetaProxy());
        }

        public override string ToString()
        {
            var stringDisplayable = Value.Concept.StringDisplayable;

            if (stringDisplayable != null)
                return stringDisplayable.conceptInterface.ToDisplayString(Value.Raw, metadata?.Raw);

            if (Value.TryGetIntrinsicValue(out var intrinsicValue) == HRESULT.S_OK && intrinsicValue != null && !Marshal.IsComObject(intrinsicValue))
                return intrinsicValue.ToString();

            return base.ToString();
        }
    }
}
