using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Symbols;
using ChaosLib.TypedData;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a "true" native frame in a call stack.<para/>
    /// Instances of this type are not backed by a <see cref="CorDebugFrame"/>. Rather, they
    /// have been completely "synthesized" by performing a manual stack walk where the V3 stack walker has informed
    /// us that one or more native frames should exist.
    /// </summary>
    public class CordbNativeFrame : CordbFrame
    {
        /// <summary>
        /// Gets the display name of this frame.
        /// </summary>
        public override string Name { get; }

        /// <summary>
        /// Gets the symbol that is associated with this frame, if one exists.
        /// </summary>
        public IDisplacedSymbol Symbol { get; }

        public bool IsInline { get; }

        /// <summary>
        /// Gets the instruction pointer of this frame. It is expected that this value is the same value stored in the <see cref="CordbFrame.Context"/> of this frame.
        /// </summary>
        public long FrameIP { get; }

        public override long FrameSP { get; }

        /// <summary>
        /// Gets the base pointer of this frame. This value may or may not be the same as the BP stored in the <see cref="CordbFrame.Context"/> of this frame.<para/>
        /// I think this means that this is the deterministic vaue of the "root" of the frame, prior to anything inside the function.<para/>
        /// When retrieving local variables, this value should be used over the BP stored in the frame context. This is particularly important on x64.
        /// </summary>
        public override long FrameBP { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CordbNativeFrame"/> class.
        /// </summary>
        /// <param name="nativeFrame">The raw native frame that underpins this <see cref="CordbNativeFrame"/>.</param>
        /// <param name="thread">The thread that the frame was retrieved from.</param>
        /// <param name="module">The module that the frame's symbol belongs to. If no symbol could be found, this value may be <see langword="null"/>.</param>
        internal CordbNativeFrame(NativeFrame nativeFrame, CordbThread thread, CordbModule module) : base(null, thread, module, nativeFrame.Context)
        {
            string name;

            if (nativeFrame.FunctionName != null)
                name = nativeFrame.FunctionName;
            else if (nativeFrame.Module != null)
                name = $"{nativeFrame.Module}!{nativeFrame.FrameIP:X}";
            else
                name = null;

            Name = name;
            Symbol = nativeFrame.Symbol;
            IsInline = nativeFrame.IsInline;

            FrameIP = nativeFrame.FrameIP;
            FrameSP = nativeFrame.FrameSP;
            FrameBP = nativeFrame.FrameBP;
        }

        #region Variable

        public override CordbVariable[] Variables
        {
            get
            {
                //First, get all symbols from DbgHelp

                //Ensure we've loaded the module first
                if (Thread.Process.Symbols.TryGetModuleBase(Context.IP, out var moduleBase))
                    Thread.Process.Symbols.EnsureModuleLoaded(moduleBase, true);

                var symbols = Thread.Process.Symbols.WithFrameContext(
                    Context.IP,
                    s => s.NativeSymEnumSymbols(null, v => v.Flags.HasFlag(SymFlag.Local))
                );

                var results = new List<CordbVariable>();

                foreach (var symbol in symbols.Cast<DbgHelpSymbol>())
                {
                    //Where does this variable live?

                    if (symbol.Flags.HasFlag(SymFlag.RegRel)) //The variable is relative to a register
                        results.Add(GetRegisterRelativeVariable(symbol));
                    else if (symbol.Flags.HasFlag(SymFlag.Register))
                        results.Add(GetRegisterVariable(symbol));
                    else if (symbol.Flags.HasFlag(SymFlag.FrameRel))
                        throw new NotImplementedException($"Handling a variable from a symbol with flag {SymFlag.FrameRel} is not implemented");
                    else if (symbol.Flags.HasFlag(SymFlag.TlsRel))
                        throw new NotImplementedException($"Handling a variable from a symbol with flag {SymFlag.TlsRel} is not implemented");
                    else if (symbol.Flags.HasFlag(SymFlag.Null))
                    {
                        //Location information is unavailable

                        var value = new DbgRemoteMissingValue(symbol.Address);

                        if (symbol.Flags.HasFlag(SymFlag.Parameter))
                            results.Add(new CordbNativeParameterVariable(symbol, value));
                        else
                            results.Add(new CordbNativeLocalVariable(symbol, value));
                    }
                    else
                    {
                        throw new NotImplementedException($"Don't know how to retrieve variable for symbol with flags {symbol.Flags}");
                    }
                }

                return results.ToArray();
            }
        }

        private CordbVariable GetRegisterRelativeVariable(DbgHelpSymbol symbol)
        {
            //Based on DbgEng DumpTypeAndReturnInfo, TranslateAddress, MachineInfo::CvRegToMachine

            /* Trying to look at how DbgEng does this, at first it seemed like there was a a separate concept of pulling the register value
             * from the "frame" when its CV_ALLREG_VFRAME and pulling the register value from the "active thread context" otherwise...but
             * in the end my final implementation for both was still the same. I think it might be confusing because DbgEng has a separate
             * concept of the current "scope" which you can mess with using the .frame command. In any case, ToIcedRegister() will convert
             * CV_ALLREG_VFRAME to the correct register based on the target platform */

            var register = symbol.Register.ToIcedRegister(Thread.Process.MachineType);
            var registerValue = Context.GetRegisterValue(register);

            //The symbol's address will be an offset from the target register
            var address = registerValue + symbol.Address;

            return CreateVariableValue(address, symbol, false);
        }

        private CordbVariable GetRegisterVariable(DbgHelpSymbol symbol)
        {
            var register = symbol.Register.ToIcedRegister(Thread.Process.MachineType);
            var registerValue = Context.GetRegisterValue(register);

            return CreateVariableValue(registerValue, symbol, true);
        }

        private CordbNativeVariable CreateVariableValue(long addressOrValue, DbgHelpSymbol symbol, bool possibleLiteral)
        {
            var value = symbol.GetValue(addressOrValue, possibleLiteral);

            if (symbol.Flags.HasFlag(SymFlag.Parameter))
                return new CordbNativeParameterVariable(symbol, value);

            return new CordbNativeLocalVariable(symbol, value);
        }

        #endregion
    }
}
