using System.ComponentModel;
using System.Diagnostics;
using ChaosDbg.Commands;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents, and provides access to manipulating the individual bits inside the DR7 debug register.
    /// </summary>
    [DebuggerDisplay("{value}")]
    public struct DR7
    {
        [CommandParser(typeof(DescriptionToEnumCommandParser<>))]
        public enum Kind : byte
        {
            [Description("e")]
            Execute = 0,

            [Description("w")]
            Write = 1,

            [Description("i")]
            IO = 2,

            [Description("r")]
            ReadWrite = 4
        }

        [CommandParser(typeof(TrimUnderscoreEnumCommandParser<>))]
        public enum Length : byte
        {
            _1 = 0,
            _2 = 1,
            _8 = 2,
            _4 = 3
        }

        //On both x32 and x64 bits 32-63 are unused
        private BitVector value;

        /// <summary>
        /// Specifies whether breakpoint 0 is locally enabled.
        /// </summary>
        public bool L0
        {
            get => value[0];
            set => this.value[0] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 0 is globally enabled.
        /// </summary>
        public bool G0
        {
            get => value[1];
            set => this.value[1] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 1 is locally enabled.
        /// </summary>
        public bool L1
        {
            get => value[2];
            set => this.value[2] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 1 is globally enabled.
        /// </summary>
        public bool G1
        {
            get => value[3];
            set => this.value[3] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 2 is locally enabled.
        /// </summary>
        public bool L2
        {
            get => value[4];
            set => this.value[4] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 2 is globally enabled.
        /// </summary>
        public bool G2
        {
            get => value[5];
            set => this.value[5] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 3 is locally enabled.
        /// </summary>
        public bool L3
        {
            get => value[6];
            set => this.value[6] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 3 is globally enabled.
        /// </summary>
        public bool G3
        {
            get => value[7];
            set => this.value[7] = value;
        }

        /// <summary>
        /// Specifies whether The locally defined breakpoints should be "exact", raising a trap-type exception immediately by the processor
        /// without delay. On processors newer than the 80386, <see cref="LE"/> and <see cref="GE"/> are ignored by the CPU, but are recommended
        /// to still be set (where used).
        /// </summary>
        public bool LE
        {
            get => value[8];
            set => this.value[8] = value;
        }

        /// <summary>
        /// Specifies whether The globally defined breakpoints should be "exact", raising a trap-type exception immediately by the processor
        /// without delay. On processors newer than the 80386, <see cref="LE"/> and <see cref="GE"/> are ignored by the CPU, but are recommended
        /// to still be set (where used).
        /// </summary>
        public bool GE
        {
            get => value[9];
            set => this.value[9] = value;
        }

        /// <summary>
        /// Reserved. Must be written as 1.
        /// </summary>
        public bool Reserved1
        {
            get => value[10];
            set => this.value[10] = value;
        }

        /// <summary>
        /// On processors with Intel TSX, enables advanced debugging of RTM transactions. Otherwise, must be 0.
        /// </summary>
        public bool RTM
        {
            get => value[11];
            set => this.value[11] = value;
        }

        /// <summary>
        /// On 386/486 processors, specifies the action to perform on a breakpoint match (0: int 1 (#DB exception), 1: break to ICE/SMM).
        /// On all other processors, reserved and must be 0.
        /// </summary>
        public bool IR_SMIE
        {
            get => value[12];
            set => this.value[12] = value;
        }

        /// <summary>
        /// Specifies whether General Detect Enable is active; if set, a debug exception will occur on any attempt at accessing DR0-DR7.
        /// </summary>
        public bool GD
        {
            get => value[13];
            set => this.value[13] = value;
        }

        /// <summary>
        /// Reserved. Must be all 0s.
        /// </summary>
        public byte Reserved2
        {
            get => (byte) value[14, 15];
            set => this.value[14, 15] = value;
        }

        /// <summary>
        /// Specifies the condition for breakpoint 0.
        /// </summary>
        public Kind RW0
        {
            get => (Kind) value[16, 17];
            set => this.value[16, 17] = (byte) value;
        }

        public Length LEN0
        {
            get => (Length) value[18, 19];
            set => this.value[18, 19] = (byte) value;
        }

        /// <summary>
        /// Specifies the condition for breakpoint 1.
        /// </summary>
        public Kind RW1
        {
            get => (Kind) value[20, 21];
            set => this.value[20, 21] = (byte) value;
        }

        /// <summary>
        /// Specifies the length of breakpoint 1.
        /// </summary>
        public Length LEN1
        {
            get => (Length) value[22, 23];
            set => this.value[22, 23] = (byte) value;
        }

        /// <summary>
        /// Specifies the condition for breakpoint 2.
        /// </summary>
        public Kind RW2
        {
            get => (Kind) value[24, 25];
            set => this.value[24, 25] = (byte) value;
        }

        /// <summary>
        /// Specifies the length of breakpoint 2.
        /// </summary>
        public Length LEN2
        {
            get => (Length) value[26, 27];
            set => this.value[26, 27] = (byte) value;
        }

        /// <summary>
        /// Specifies the condition for breakpoint 3.
        /// </summary>
        public Kind RW3
        {
            get => (Kind) value[28, 29];
            set => this.value[28, 29] = (byte) value;
        }

        /// <summary>
        /// Specifies the length of breakpoint 3.
        /// </summary>
        public Length LEN3
        {
            get => (Length) value[30, 31];
            set => this.value[30, 31] = (byte) value;
        }

        //Bits 32-63 for x64 not stored. Must be all 0's

        public static implicit operator DR7(uint value)
        {
            return new DR7
            {
                value = new BitVector(value)
            };
        }

        public static implicit operator int(DR7 value) => value.value;

        public void ClearBreakpoints()
        {
            /* There can be random values in the register when we first receive it. There are 3 main sections of the Dr7 register:
             * - Bits 0-7: local/global enable BP status
             * - Bits 8-15: common settings
             * - Bits 16-31: BP conditions/lengths
             *
             * We clear out everything but bits 8-15 */
            value = new BitVector((uint) (value & ~0xffff00ff));
        }
    }
}
