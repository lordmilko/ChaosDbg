using System;
using System.Runtime.InteropServices;
using ChaosLib.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataField
    {
        public mdFieldDef FieldDef { get; }

        public string Name { get; }

        public CorFieldAttr Flags { get; }

        public IWindowsMetadataType Type { get; }

        public WindowsMetadataType Owner { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public object Value { get; }

        public WindowsMetadataField(WindowsMetadataType owner, mdFieldDef fieldDef, GetFieldPropsResult props, IWindowsMetadataType type, ISigCustomAttribute[] customAttributes)
        {
            Owner = owner;
            FieldDef = fieldDef;
            Name = props.szField;
            Flags = props.pdwAttr;
            Type = type;
            CustomAttributes = customAttributes;

            if (type is WindowsMetadataPrimitiveType p)
                Value = ReadPrimitiveType(p.Type, props.ppValue,  props.pcchValue);
            else
            {
                if (props.ppValue != IntPtr.Zero)
                {
                    if (type is WindowsMetadataTransparentType t)
                    {
                        var underlying = t.ValueField.Type;

                        if (underlying is WindowsMetadataPointerType)
                            Value = ReadValue<IntPtr>(props.ppValue);
                        else if (underlying is WindowsMetadataPrimitiveType tp)
                            Value = ReadPrimitiveType(tp.Type, props.ppValue, props.pcchValue);
                        else
                            throw new InvalidOperationException($"Cannot read default value for type {underlying.GetType().Name} inside transparent struct representing type {type}");
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot read default value for field type {type.GetType().Name} representing type {type}");
                    }
                }
            }            
        }

        private object ReadPrimitiveType(CorElementType type, IntPtr ptr, int length)
        {
            switch (type)
            {
                case CorElementType.Boolean:
                    return ReadValue<byte>(ptr) == 1;

                case CorElementType.Char:
                    return ReadValue<char>(ptr);

                case CorElementType.I1:
                    return ReadValue<sbyte>(ptr);

                case CorElementType.U1:
                    return ReadValue<byte>(ptr);

                case CorElementType.I2:
                    return ReadValue<short>(ptr);

                case CorElementType.U2:
                    return ReadValue<ushort>(ptr);

                case CorElementType.I4:
                    return ReadValue<int>(ptr);

                case CorElementType.U4:
                    return ReadValue<uint>(ptr);

                case CorElementType.I8:
                    return ReadValue<long>(ptr);

                case CorElementType.U8:
                    return ReadValue<ulong>(ptr);

                case CorElementType.R4:
                    return ReadValue<float>(ptr);

                case CorElementType.R8:
                    return ReadValue<double>(ptr);

                case CorElementType.String:
                    //We expect most values that have a string to be constants, but *.winmd files can also
                    //contain Attributes, and they might have string members without default values
                    if (ptr != IntPtr.Zero)
                        return Marshal.PtrToStringUni(ptr, length);

                    return null;

                case CorElementType.I:
                    return ReadValue<IntPtr>(ptr);

                case CorElementType.U:
                    return ReadValue<UIntPtr>(ptr);

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(CorElementType)} '{type}'.");
            }
        }

        private unsafe T? ReadValue<T>(IntPtr ptr) where T : unmanaged
        {
            if (ptr == IntPtr.Zero)
                return null;

            var value = *(T*)ptr;
            return value;
        }

        public override string ToString()
        {
            if (Value == null)
                return $"{Type} {Name}";

            return $"{Type} {Name} = {(Value is string ? $"\"{Value}\"" : Value)}";
        }
    }
}
