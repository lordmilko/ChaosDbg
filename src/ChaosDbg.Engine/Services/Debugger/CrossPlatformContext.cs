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

        public bool IsX86 => Flags >= ContextFlags.X86Context && Flags <= ContextFlags.X86ContextAll;

        public bool IsAmd64 => Flags >= ContextFlags.AMD64Context && Flags <= ContextFlags.AMD64ContextAll;

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
                    Raw.X86Context.Eip = (int) value;
                else if (IsAmd64)
                    Raw.Amd64Context.Rip = value;
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

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
                    Raw.X86Context.Esp = (int) value;
                else if (IsAmd64)
                    Raw.Amd64Context.Rsp = value;
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

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
                    Raw.X86Context.Ebp = (int) value;
                else if (IsAmd64)
                    Raw.Amd64Context.Rbp = value;
                else
                    throw new NotImplementedException($"Don't know how to handle flags '{Flags}'.");
            }
        }

        //CONTEXT is a massive data structure - over 1200 bytes! We want to avoid copying it around as much as possible. Thus,
        //this is a field instead of a property
        public CROSS_PLATFORM_CONTEXT Raw;

        public CrossPlatformContext(ContextFlags flags, in CROSS_PLATFORM_CONTEXT context)
        {
            Flags = flags;
            Raw = context;
        }
    }
}
