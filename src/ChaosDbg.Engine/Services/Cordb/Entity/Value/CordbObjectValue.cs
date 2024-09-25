using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private readonly MetadataType metadataType;

        public MetadataType MetadataType
        {
            get
            {
                ThrowIfStale();
                return metadataType;
            }
        }

        private Dictionary<string, CordbValue> fieldMap = new();
        private CordbValue[]? fields;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
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

                        fieldMap = fields.ToDictionary(f => f.Symbol.Name, f => f);
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

        public override CordbValue this[string fieldName]
        {
            get
            {
                ThrowIfStale();

                //Ensure that the fieldMap is initialized
                _ = Fields;

                if (!fieldMap.TryGetValue(fieldName, out var value))
                    throw new InvalidOperationException($"Could not find field '{fieldName}' on type '{MetadataType.Name}'.");

                return value;
            }
        }

        public override string ToString()
        {
            return MetadataType.ToString();
        }
    }
}
