using System;
using System.Diagnostics;
using ClrDebug;

namespace ChaosDbg
{
    /// <summary>
    /// Boxes a <see cref="CROSS_PLATFORM_CONTEXT"/> and provides facilities for
    /// retrieving key registers in a cross-platform compatible way.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class CrossPlatformContext
    {
        private string DebuggerDisplay => $"IP = {IP:X}, SP = {SP:X}, BP = {BP:X}";

        /// <summary>
        /// Gets the flags that were used to initialize the context.
        /// </summary>
        public ContextFlags Flags;

        /// <summary>
        /// Gets whether this context was created from <see cref="ContextFlags.X86Context"/> related flags.
        /// </summary>
        public bool IsX86 => Flags >= ContextFlags.X86Context && Flags <= ContextFlags.X86ContextAll;

        /// <summary>
        /// Gets whether this context was created from <see cref="ContextFlags.AMD64Context"/> related flags.
        /// </summary>
        public bool IsAmd64 => Flags >= ContextFlags.AMD64Context && Flags <= ContextFlags.AMD64ContextAll;

        /// <summary>
        /// Gets or sets whether any fields on the register context have been modified. This property will automatically be updated
        /// for any properties modified on this <see cref="CrossPlatformContext"/> object. If any fields are modified on the underlying
        /// <see cref="CROSS_PLATFORM_CONTEXT"/> directly, this property must be manually set.
        /// </summary>
        public bool IsModified { get; set; }

        /// <summary>
        /// Gets the instruction pointer of this context (<see cref="Register.EIP"/> for x86, <see cref="Register.RIP"/> for x64).
        /// </summary>
        public long IP
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Eip;

                if (IsAmd64)
                    return Raw.Amd64Context.Rip;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Eip, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Rip, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        /// <summary>
        /// Gets the stack pointer of this context (<see cref="Register.ESP"/> for x86, <see cref="Register.RSP"/> for x64).
        /// </summary>
        public long SP
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Esp;

                if (IsAmd64)
                    return Raw.Amd64Context.Rsp;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Esp, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Rsp, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        /// <summary>
        /// Gets the base pointer of this context (<see cref="Register.EBP"/> for x86, <see cref="Register.RBP"/> for x64).
        /// </summary>
        public long BP
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Ebp;

                if (IsAmd64)
                    return Raw.Amd64Context.Rbp;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Ebp, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Rbp, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public X86_CONTEXT_FLAGS EFlags
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.EFlags;

                if (IsAmd64)
                    return Raw.Amd64Context.EFlags;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.EFlags, value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.EFlags, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        #region Debug Registers

        public long Dr0
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Dr0;

                if (IsAmd64)
                    return Raw.Amd64Context.Dr0;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr0, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr0, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public long Dr1
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Dr1;

                if (IsAmd64)
                    return Raw.Amd64Context.Dr1;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr1, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr1, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public long Dr2
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Dr2;

                if (IsAmd64)
                    return Raw.Amd64Context.Dr2;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr2, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr2, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public long Dr3
        {
            get
            {
                if (IsX86)
                    return Raw.X86Context.Dr3;

                if (IsAmd64)
                    return Raw.Amd64Context.Dr3;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr3, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr3, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public DR6 Dr6
        {
            get
            {
                if (IsX86)
                    return (uint) Raw.X86Context.Dr6;

                if (IsAmd64)
                    return (uint) Raw.Amd64Context.Dr6;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr6, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr6, (long) value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        public DR7 Dr7
        {
            get
            {
                if (IsX86)
                    return (uint) Raw.X86Context.Dr7;

                if (IsAmd64)
                    return (uint) Raw.Amd64Context.Dr7;

                throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
            set
            {
                if (IsX86)
                    SetValue(ref Raw.X86Context.Dr7, (int) value);
                else if (IsAmd64)
                    SetValue(ref Raw.Amd64Context.Dr7, value);
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        #endregion

        //CONTEXT is a massive data structure - over 1200 bytes! We want to avoid copying it around as much as possible. Thus,
        //this is a field instead of a property
        public CROSS_PLATFORM_CONTEXT Raw;

        public CrossPlatformContext(ContextFlags flags, in CROSS_PLATFORM_CONTEXT context)
        {
            Flags = flags;
            Raw = context;
        }

        private void SetValue<T>(ref T field, T value)
        {
            //If the values are already equal, no need to do anything
            if (field.Equals(value))
                return;

            field = value;

            //Flag the context as modified so that we update it when the debugger resumes
            IsModified = true;
        }
    }
}
