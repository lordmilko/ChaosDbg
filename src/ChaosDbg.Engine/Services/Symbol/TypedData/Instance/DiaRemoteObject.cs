using System;
using System.Diagnostics;
using ChaosLib.TypedData;
using ClrDebug.DIA;

namespace ChaosDbg.TypedData
{
    /// <summary>
    /// Represents a complex object that exists in a remote process whose type is underpinned by a <see cref="DiaSymbol"/>.
    /// </summary>
    class DiaRemoteObject : IDbgRemoteObject
    {
        public DiaRemoteType Type { get; }

        public long Address { get; }

        public DiaRemoteFieldCollection Fields { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        object IDbgRemoteValue.Value
        {
            get => this;
            set => throw new NotImplementedException();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected ITypedDataProvider provider;

        public DiaRemoteObject(DiaRemoteType type, long address, ITypedDataProvider provider)
        {
            Type = type;
            Address = address;
            this.provider = provider;

            Fields = new DiaRemoteFieldCollection(type, provider);
        }

        public IDbgRemoteValue this[string name]
        {
            get
            {
                var fieldInfo = Type.Fields[name];

                if (fieldInfo.Offset == null)
                    throw new NotImplementedException();

                return provider.CreateValue(Address + fieldInfo.Offset.Value, fieldInfo.Type, Type);
            }
        }

        public bool IsEquivalentTo(object other)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return $"{Type} : 0x{Address:X}";
        }
    }
}
