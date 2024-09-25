using System;
using System.Text;
using ChaosDbg.Disasm;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a managed parameter or local variable defined within a function.
    /// </summary>
    public abstract class CordbManagedVariable : CordbVariable
    {
        public CordbILFrame Frame { get; }

        public CorDebugVariableHome CorDebugVariableHome { get; }

        public VariableLocationType LocationType { get; }

        /// <summary>
        /// Gets the absolute memory address at which this variable begins.
        /// </summary>
        public CORDB_ADDRESS StartAddress { get; internal set; }

        /// <summary>
        /// Gets the absolute memory address at which this variable ends.
        /// </summary>
        public CORDB_ADDRESS EndAddress { get; internal set; }

        /// <summary>
        /// Gets the amount of memory that this variable occupies.
        /// </summary>
        public int Length => (int) (EndAddress - StartAddress);

        /// <summary>
        /// Gets the register that this variable is contained in. If <see cref="LocationType"/> is <see cref="VariableLocationType.VLT_REGISTER_RELATIVE"/>,
        /// this variable is contained at a memory address pointed to by an <see cref="Offset"/> from this variable (e.g. [rbp+10h])
        /// </summary>
        public Register Register { get; }

        /// <summary>
        /// If <see cref="LocationType"/> is <see cref="VariableLocationType.VLT_REGISTER_RELATIVE"/>, gets the offset from <see cref="Register"/> that points
        /// to the variable (e.g. [rbp+10h])
        /// </summary>
        public int Offset { get; }

        private CordbValue value;

        public CordbValue Value
        {
            get
            {
                if (value == null)
                    value = GetValue();

                return value;
            }
        }

        protected CordbManagedVariable(
            CorDebugVariableHome corDebugVariableHome,
            CordbILFrame frame,
            CordbModule module)
        {
            CorDebugVariableHome = corDebugVariableHome;
            Frame = frame;

            LocationType = corDebugVariableHome.LocationType;

            var arch = module.Process.MachineType;

            switch (LocationType)
            {
                case VariableLocationType.VLT_REGISTER:
                    Register = corDebugVariableHome.Register.ToIcedRegister(arch);
                    break;

                case VariableLocationType.VLT_REGISTER_RELATIVE:
                    Register = corDebugVariableHome.Register.ToIcedRegister(arch);
                    Offset = corDebugVariableHome.Offset;
                    break;

                case VariableLocationType.VLT_INVALID:
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(VariableLocationType)} '{LocationType}'.");
            }
        }

        protected abstract CordbValue GetValue();

        public override string ToString()
        {
            switch (LocationType)
            {
                case VariableLocationType.VLT_REGISTER:
                    return Register.ToString().ToLower();

                case VariableLocationType.VLT_REGISTER_RELATIVE:
                    var builder = new StringBuilder();

                    builder.Append("[").Append(Register.ToString().ToLower());

                    if (Offset < 0)
                        builder.Append("-").Append((-Offset).ToString("x2"));
                    else
                        builder.Append("+").Append(Offset.ToString("x2"));

                    builder.Append("h]");

                    return builder.ToString();

                default:
                    return LocationType.ToString();
            }
        }
    }
}
