using System.Diagnostics;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class DbgEngFrame
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected virtual string DebuggerDisplay
        {
            get
            {
                if (Name != null)
                    return Name;

                //There is no BP field on DEBUG_STACK_FRAME
                return string.Join(", ", new[]
                {
                    $"IP = 0x{IP:X}",
                    $"SP = 0x{SP:X}"
                });
            }
        }

        public string Name { get; }

        public long IP { get; }

        public long SP { get; }

        public DbgEngFrame(string name, DEBUG_STACK_FRAME frame)
        {
            Name = name;
            IP = frame.InstructionOffset;
            SP = frame.StackOffset;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
