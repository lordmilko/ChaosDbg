using System.Diagnostics;
using System.Text;
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
                var builder = new StringBuilder();

                if (frame.InlineFrameContext.FrameType.HasFlag(STACK_FRAME_TYPE.STACK_FRAME_TYPE_INLINE))
                    builder.Append("[Inline] ");

                var name = Name;

                if (name != null)
                    builder.Append(name);
                else
                {
                    //There is no BP field on DEBUG_STACK_FRAME
                    builder.Append(string.Join(", ", new[]
                    {
                        $"IP = 0x{IP:X}",
                        $"SP = 0x{SP:X}"
                    }));
                }

                return builder.ToString();
            }
        }

        public string Name { get; }

        public long IP { get; }

        public long SP { get; }

        private DEBUG_STACK_FRAME_EX frame;

        public DbgEngFrame(string name, in DEBUG_STACK_FRAME_EX frame)
        {
            Name = name;
            IP = frame.InstructionOffset;
            SP = frame.StackOffset;
            this.frame = frame;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
