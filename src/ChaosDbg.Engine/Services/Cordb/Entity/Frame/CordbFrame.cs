using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public abstract class CordbFrame<T> : CordbFrame where T : CorDebugFrame
    {
        public new T CorDebugFrame => (T) base.CorDebugFrame;

        protected CordbFrame(CorDebugFrame corDebugFrame, CrossPlatformContext context) : base(corDebugFrame, context)
        {
        }
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class CordbFrame
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        protected virtual string DebuggerDisplay
        {
            get
            {
                if (Name != null)
                    return Name;

                return string.Join(", ", new[]
                {
                    $"IP = 0x{Context.IP:X}",
                    $"SP = 0x{Context.SP:X}",
                    $"BP = 0x{Context.BP:X}"
                });
            }
        }

        public CorDebugFrame CorDebugFrame { get; }

        public CrossPlatformContext Context { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string name;

        public virtual string Name
        {
            get
            {
                if (name == null && CorDebugFrame != null && !(this is CordbRuntimeNativeFrame))
                    name = CordbFormatter.FunctionName(CorDebugFrame.Function);

                return name;
            }
        }

        protected CordbFrame(CorDebugFrame corDebugFrame, CrossPlatformContext context)
        {
            CorDebugFrame = corDebugFrame;
            Context = context;
        }

        public override string ToString() => Name;
    }
}
