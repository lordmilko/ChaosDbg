using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public abstract class CordbFrame<T> : CordbFrame where T : CorDebugFrame
    {
        public new T CorDebugFrame => (T) base.CorDebugFrame;

        protected CordbFrame(CorDebugFrame corDebugFrame, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, module, context)
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
                    name = ((CordbManagedModule) Module).MetaDataProvider.ResolveMethodDef(CorDebugFrame.FunctionToken).ToString();

                return name;
            }
        }

        /// <summary>
        /// Gets the module associated with this frame, or null if the module is in dynamically generated code.
        /// </summary>
        public CordbModule Module { get; }

        protected CordbFrame(CorDebugFrame corDebugFrame, CordbModule module, CrossPlatformContext context)
        {
            CorDebugFrame = corDebugFrame;
            Module = module;
            Context = context;
        }

        public override string ToString() => Name ?? $"[{GetType().Name}] 0x{Context.IP:X}";
    }
}
