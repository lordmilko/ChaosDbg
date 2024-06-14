using System.Diagnostics;
using ChaosDbg.Disasm;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a stack frame in a managed process that is backed by some type of <see cref="ClrDebug.CorDebugFrame"/> object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class CordbFrame<T> : CordbFrame where T : CorDebugFrame
    {
        public new T CorDebugFrame => (T) base.CorDebugFrame!;

        protected CordbFrame(CorDebugFrame corDebugFrame, CordbThread thread, CordbModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
        }
    }

    /// <summary>
    /// Represents a stack frame in a managed process.<para/>
    /// This type is sub-classed for different types of stack frames (managed, transition, unmanaged, etc) that may exist in a stack trace.
    /// </summary>
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

        /// <summary>
        /// Gets the <see cref="ClrDebug.CorDebugFrame"/> that underpins this frame.<para/>
        /// If this frame is not known to ICorDebug (e.g. this is a native frame that is not a transition frame (<see cref="CorDebugNativeFrame"/>)
        /// then this value may be <see langword="null"/>.
        /// </summary>
        public CorDebugFrame? CorDebugFrame { get; }

        /// <summary>
        /// Gets the register context that is associated with this frame.
        /// </summary>
        public CrossPlatformContext Context { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string? name;

        public virtual string? Name
        {
            get
            {
                if (name == null && CorDebugFrame != null && Module != null && !(this is CordbRuntimeNativeFrame))
                    name = ((CordbManagedModule) Module).MetaDataProvider.ResolveMethodDef(CorDebugFrame.FunctionToken).ToString();

                return name;
            }
        }

        /// <summary>
        /// Gets the next instruction that will be executed by this frame (when control returns to it if it
        /// is not at the top of the stack). If the current frame has called into another function, this shows
        /// the address of the next instruction after that call instruction.
        /// </summary>
        public INativeInstruction NextInstruction => Thread.Process.ProcessDisassembler.Disassemble(Context.IP);

        /// <summary>
        /// Gets the thread that this frame is associated with.
        /// </summary>
        public CordbThread Thread { get; }

        /// <summary>
        /// Gets the module associated with this frame, or null if a module could not be found or the module is in dynamically generated code.
        /// </summary>
        public CordbModule? Module { get; }

        //When doing a native stack walk, we can have different values for BP on the frame vs the context. For managed frames,
        //CorDebugFrame doesn't provide this information, so we'll just go with what's in the context
        public virtual long FrameSP => Context.SP;

        public virtual long FrameBP => Context.BP;

        /// <summary>
        /// Gets all variables (parameters and locals) that may be associated with this frame.
        /// </summary>
        public abstract CordbVariable[] Variables { get; }

        protected CordbFrame(CorDebugFrame? corDebugFrame, CordbThread thread, CordbModule? module, CrossPlatformContext context)
        {
            CorDebugFrame = corDebugFrame;
            Thread = thread;
            Module = module;
            Context = context;
        }

        public override string ToString() => Name ?? $"[{GetType().Name}] 0x{Context.IP:X}";
    }
}
