using System;
using System.Reflection;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a primative value from the address space of a debug target.
    /// </summary>
    class CordbPrimativeValue : CordbValue
    {
        public new CorDebugGenericValue CorDebugValue => (CorDebugGenericValue) base.CorDebugValue;

        private object? clrValue;

        /// <summary>
        /// Gets the <see cref="bool"/>, <see cref="char"/>, <see cref="sbyte"/>, <see cref="byte"/>,
        /// <see cref="short"/>, <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>,
        /// <see cref="ulong"/>, <see cref="float"/>, <see cref="double"/>, <see cref="IntPtr"/> or <see cref="UIntPtr"/>
        /// value that is associated with this object.
        /// </summary>
        public unsafe object ClrValue
        {
            get
            {
                ThrowIfStale();

                if (clrValue == null)
                {
                    var size = CorDebugValue.Size64;

                    //I don't really understand how a primative value could have a size greater than uint.MaxValue,
                    //but if that is the case, we can't pass a long to Marshal.AllocHGlobal
                    var bytes = new byte[size];

                    fixed (byte* p = &bytes[0])
                    {
                        CorDebugValue.GetValue((IntPtr) p);

                        var type = CorDebugValue.Type;

                        clrValue = type switch
                        {
                            CorElementType.Boolean => *(bool*) p,
                            CorElementType.Char => (char) *(ushort*) p,
                            CorElementType.I1 => *(sbyte*) p,
                            CorElementType.U1 => *(byte*) p,
                            CorElementType.I2 => *(short*) p,
                            CorElementType.U2 => *(ushort*) p,
                            CorElementType.I4 => *(int*) p,
                            CorElementType.U4 => *(uint*) p,
                            CorElementType.I8 => *(long*) p,
                            CorElementType.U8 => *(ulong*) p,
                            CorElementType.I => *(IntPtr*) p,
                            CorElementType.U => *(UIntPtr*) p,
                            _ => throw new UnknownEnumValueException(type)
                        };
                    }
                }

                return clrValue;
            }
            set
            {
                ThrowIfStale();

                /* mscordbi!CordbGenericValue::SetValue sets the value if the VariableHome if the CordbGenericValue is _not_
                 * m_isLiteral, and then also updates the value stored in m_pCopyOfData. m_pCopyOfData is a local 8-byte buffer
                 * that stores the local value. A CordvGenericValue is considered to be a "literal" if it was completely made up
                 * by mscordbi and doesn't actually exist in the target process. Literal values are created by mscordbi!CordbEval
                 * (CordbEval::CreatePrimativeLiteral and CordbEval::CreateValueForType) */

                var type = CorDebugValue.Type;

                switch (type)
                {
                    case CorElementType.Boolean:
                    {
                        var val = Convert.ToBoolean(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.Char:
                    {
                        var val = Convert.ToChar(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.I1:
                    {
                        var val = Convert.ToSByte(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.U1:
                    {
                        var val = Convert.ToByte(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.I2:
                    {
                        var val = Convert.ToInt16(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.U2:
                    {
                        var val = Convert.ToUInt16(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.I4:
                    {
                        var val = Convert.ToInt32(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.U4:
                    {
                        var val = Convert.ToUInt32(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.I8:
                    {
                        var val = Convert.ToInt64(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.U8:
                    {
                        var val = Convert.ToUInt64(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.R4:
                    {
                        var val = Convert.ToSingle(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }

                    case CorElementType.R8:
                    {
                        var val = Convert.ToDouble(value);
                        CorDebugValue.SetValue((IntPtr) (&val));
                        break;
                    }
        //Note that the ICorDebugGenericValue type
        //Per mscordbi!CordbValue::CreateValueByType, BOOLEAN -> R8 + I + U are "generic" values
        internal CordbPrimativeValue(CorDebugValue corDebugValue, CordbThread thread, CordbValue? parent, MemberInfo? symbol) : base(corDebugValue, thread, parent, symbol)
        {
        }

        public override string ToString()
        {
            var val = ClrValue;

            if (val is IntPtr i)
                val = "0x" + i.ToString("X");

            if (Symbol == null)
                return val.ToString();

            return $"{Symbol} = {val}";
        }
    }
}
