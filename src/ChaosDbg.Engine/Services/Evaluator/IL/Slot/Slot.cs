using System;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Evaluator.IL
{
    /// <summary>
    /// Represents a container capable of storing a value in the evaluation stack of an <see cref="ILVirtualMachine"/> <see cref="ILVirtualFrame"/>.
    /// </summary>
    abstract class Slot
    {
        
            var type = obj.GetType();
            var typeCode = Type.GetTypeCode(type);

            switch (typeCode)
            {
                //There's no such thing as bool. Boolean values compile to either Ldc_I4_0 or Ldc_I4_1.
                //In any case, if we do a comparison, we want to return bool, not Int32. So we still need to capture the
                //fact bool is bool
                case TypeCode.Boolean:
                    return new IntegerSlot(obj, CorElementType.Boolean);

                case TypeCode.Char:
                    return new PrimitiveSlot(obj, CorElementType.Char);
                case TypeCode.SByte:
                case TypeCode.Byte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return new IntegerSlot(obj, typeCode.ToCorElementType());

                case TypeCode.Single:
                case TypeCode.Double:
                    return new FloatingPointSlot(obj, typeCode.ToCorElementType());

                case TypeCode.Object:
                    if (type.IsArray)
                        return new ArraySlot(obj, type);

                    return new ObjectSlot(obj, type);
        public object Value { get; }

        protected Slot(object value)
    }
}
