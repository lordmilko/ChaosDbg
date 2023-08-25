using System;
using System.Runtime.InteropServices;
using ChaosDbg.Metadata;
using ClrDebug;

namespace ChaosDbg.WinMD
{
    class WindowsMetadataField
    {
        public mdFieldDef FieldDef { get; }

        public string Name { get; }

        public CorFieldAttr Flags { get; }

        public IWindowsMetadataType Type { get; }

        public ISigCustomAttribute[] CustomAttributes { get; set; }

        public object Value { get; }

        public WindowsMetadataField(mdFieldDef fieldDef, GetFieldPropsResult props, IWindowsMetadataType type, ISigCustomAttribute[] customAttributes)
        {
            FieldDef = fieldDef;
            Name = props.szField;
            Flags = props.pdwAttr;
            Type = type;
            CustomAttributes = customAttributes;

            if (type is WindowsMetadataPrimitiveType p)
            {
                switch (p.Type)
                {
                    case CorElementType.Boolean:
                        Value = ReadValue<byte>(props.ppValue) == 1;
                        break;

                    case CorElementType.Char:
                        Value = ReadValue<char>(props.ppValue);
                        break;

                    case CorElementType.I1:
                        Value = ReadValue<sbyte>(props.ppValue);
                        break;

                    case CorElementType.U1:
                        Value = ReadValue<byte>(props.ppValue);
                        break;

                    case CorElementType.I2:
                        Value = ReadValue<short>(props.ppValue);
                        break;

                    case CorElementType.U2:
                        Value = ReadValue<ushort>(props.ppValue);
                        break;

                    case CorElementType.I4:
                        Value = ReadValue<int>(props.ppValue);
                        break;

                    case CorElementType.U4:
                        Value = ReadValue<uint>(props.ppValue);
                        break;

                    case CorElementType.I8:
                        Value = ReadValue<long>(props.ppValue);
                        break;

                    case CorElementType.U8:
                        Value = ReadValue<ulong>(props.ppValue);
                        break;

                    case CorElementType.R4:
                        Value = ReadValue<float>(props.ppValue);
                        break;

                    case CorElementType.R8:
                        Value = ReadValue<double>(props.ppValue);
                        break;

                    case CorElementType.String:
                        //We expect most values that have a string to be constants, but *.winmd files can also
                        //contain Attributes, and they might have string members without default values
                        if (props.ppValue != IntPtr.Zero)
                            Value = Marshal.PtrToStringUni(props.ppValue, props.pcchValue);
                        break;

                    case CorElementType.I:
                        Value = ReadValue<IntPtr>(props.ppValue);
                        break;

                    case CorElementType.U:
                        Value = ReadValue<UIntPtr>(props.ppValue);
                        break;

                    default:
                        throw new NotImplementedException($"Don't know how to handle {nameof(CorElementType)} '{p.Type}'.");
                }
            }
            else
            {
                if (props.pcchValue != 0)
                {
                    if (!(type is WindowsMetadataPrimitiveType t))
                        throw new InvalidOperationException($"Cannot read default value for field type {type.GetType().Name} representing type {type}");
                }
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
            return $"{Type} {Name}";
        }
    }
}
