using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Disasm;
using ChaosLib;
using ChaosLib.Metadata;
using ChaosLib.TypedData;
using ClrDebug;
using ClrDebug.DIA;
using Iced.Intel;

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
                Thread.Process.Symbols.EnsureModuleLoaded(Context.IP, true);

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

            long registerValue;

            switch (symbol.Register)
            {
                case CV_HREG_e.CV_ALLREG_VFRAME:
                    //Pull the data from the frame
                    if (Thread.Process.Is32Bit)
                        registerValue = Context.GetRegisterValue(Register.EBP);
                    else
                        registerValue = Context.GetRegisterValue(Register.RSP); //Based on dbgeng!MachineInfo::CvRegToMachine
                    break;

                default:
                    //Pull the data from the active thread context
                    var register = symbol.Register.ToIcedRegister(Thread.Process.MachineType);

                    registerValue = Context.GetRegisterValue(register);
                    break;
            }

            //The symbol's address will be an offset from the target register
            var address = registerValue + symbol.Address;

            return CreateVariableValue(address, symbol);
        }

        private CordbVariable GetRegisterVariable(DbgHelpSymbol symbol)
        {
            var register = symbol.Register.ToIcedRegister(Thread.Process.MachineType);
            var registerValue = Context.GetRegisterValue(register);

            return CreateVariableValue(registerValue, symbol);
        }

        private CordbNativeVariable CreateVariableValue(long addressOrValue, DbgHelpSymbol symbol)
        {
            var value = symbol.GetValue(addressOrValue);

            if (symbol.Flags.HasFlag(SymFlag.Parameter))
                return new CordbNativeParameterVariable(symbol, value);

            return new CordbNativeLocalVariable(symbol, value);
        }

        #endregion
    }
}
