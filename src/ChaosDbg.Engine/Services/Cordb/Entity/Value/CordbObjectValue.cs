using System;
using System.Reflection;
using ChaosLib.Metadata;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a complex value containing fields that point to other values.
    /// </summary>
    class CordbObjectValue : CordbValue
    {
        private MetadataType metadataType;

        public MetadataType MetadataType
        {
            get
            {
                ThrowIfStale();
                return metadataType;
            }
        }

        private CordbValue[]? fields;

        public CordbValue[] Fields
        {
            get
            {
                ThrowIfStale();

                if (fields == null)
                {
                    var metadataFields = MetadataType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (metadataFields.Length == 0)
                        fields = Array.Empty<CordbValue>();
                    else
                    {
                        fields = new CordbValue[metadataFields.Length];

                        var @class = CorDebugValue.Class.Raw;

                        for (var i = 0; i < fields.Length; i++)
                        {
                            var fieldValue = CorDebugValue.GetFieldValue(@class, metadataFields[i].MetadataToken);

                            fields[i] = New(fieldValue, Thread, this, metadataFields[i]);
                        }
                    }
                }

                return fields;
            }
        }

        public new CorDebugObjectValue CorDebugValue => (CorDebugObjectValue) base.CorDebugValue;

        public CordbManagedModule Module { get; }

        internal CordbObjectValue(CorDebugObjectValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol) : base(corDebugValue, thread, parent, symbol)
        {
            var corDebugClass = corDebugValue.ExactType.Class;
            Module = thread.Process.Modules.GetModule(corDebugClass.Module);

            this.metadataType = (MetadataType) Module.MetadataModule.ResolveType(corDebugClass.Token);
        }

        public CordbValue this[string fieldName]
        {
            get
            {
                ThrowIfStale();

                var field = (MetadataFieldInfo) MetadataType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (field == null)
                    throw new InvalidOperationException($"Could not find field '{fieldName}' on type '{MetadataType.Name}'.");

                var fieldValue = CorDebugValue.GetFieldValue(CorDebugValue.Class.Raw, field.Token);

                return New(fieldValue, Thread, this, field);
            }
        }

        public override string ToString()
        {
            return MetadataType.ToString();
        }
    }
}
