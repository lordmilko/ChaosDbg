using System.Diagnostics;
using ChaosLib.Memory;

namespace ChaosDbg
{
    /// <summary>
    /// Represents, and provides access to manipulating the individual bits inside the DR6 debug register.
    /// </summary>
    [DebuggerDisplay("{value}")]
    public struct DR6
    {
        //On both x32 and x64 bits 32-63 are unused
        private BitVector value;

        /// <summary>
        /// Specifies whether breakpoint 0 in <see cref="Register.DR0"/> was hit.
        /// </summary>
        public bool B0
        {
            get => value[0];
            set => this.value[0] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 0 in <see cref="Register.DR1"/> was hit.
        /// </summary>
        public bool B1
        {
            get => value[1];
            set => this.value[1] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 0 in <see cref="Register.DR2"/> was hit.
        /// </summary>
        public bool B2
        {
            get => value[2];
            set => this.value[2] = value;
        }

        /// <summary>
        /// Specifies whether breakpoint 0 in <see cref="Register.DR3"/> was hit.
        /// </summary>
        public bool B3
        {
            get => value[3];
            set => this.value[3] = value;
        }

        /// <summary>
        /// Reserved. On 386/486 processors this value should be 0. On all other processors, it should
        /// be all 1s (6 bits: 0x7F)
        /// </summary>
        public byte Reserved1
        {
            get => (byte) value[4, 10];
            set => this.value[4, 10] = value;
        }

        /// <summary>
        /// Sets to 0 by Bus Lock Trap exceptions. On processors that don't support Bus Lock Trap exceptions, this bit is always 1.
        /// </summary>
        public bool BLD
        {
            get => value[11];
            set => this.value[11] = value;
        }

        /// <summary>
        /// On 386/486 processors, specifies whether SMM or ICE mode was entered. On all later processors this bit is reserved and should be 0.
        /// </summary>
        public bool BK_SMMS
        {
            get => value[12];
            set => this.value[12] = value;
        }

        /// <summary>
        /// Specifies whether the next instruction was detected as being one that accesses a debug register.
        /// </summary>
        public bool BD
        {
            get => value[13];
            set => this.value[13] = value;
        }

        /// <summary>
        /// Specifies whether the debug exception was triggered by a single-step exception.
        /// </summary>
        public bool BS
        {
            get => value[14];
            set => this.value[14] = value;
        }

        /// <summary>
        /// Specifies whether the debug exception was triggered by a task switch where the T (debug trap) flag was set in the TSS
        /// </summary>
        public bool BT
        {
            get => value[15];
            set => this.value[15] = value;
        }

        /// <summary>
        /// On processors with Intel TSX, set to 0 for exceptions that occur inside RTM transactions. Otherwise, will be 1.
        /// </summary>
        public bool RTM
        {
            get => value[16];
            set => this.value[16] = value;
        }

        /// <summary>
        /// Reserved. On 386/486 processors this value should be 0. On all other processors, it should
        /// be all 1s (16 bits: 0xFF)
        /// </summary>
        public int Reserved2
        {
            get => value[17, 31];
            set => this.value[17, 31] = value;
        }

        //Bits 32-63 for x64 not stored. Must be all 0's

        public static implicit operator DR6(uint value)
        {
            return new DR6
            {
                value = new BitVector(value)
            };
        }

        public static implicit operator int(DR6 value) => value.value;
    }
}
