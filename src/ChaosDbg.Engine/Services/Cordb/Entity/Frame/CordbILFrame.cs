using System.Collections.Generic;
using System.Linq;
using ClrDebug;
using SymHelp.Symbols;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents an managed/IL frame in a stack trace.<para/>
    /// Encapsulates the <see cref="CorDebugILFrame"/> type.
    /// </summary>
    public class CordbILFrame : CordbFrame<CorDebugILFrame>
    {
        public new CordbManagedModule Module => (CordbManagedModule) base.Module;

        public CordbILFunction Function { get; }

        internal CordbILFrame(CorDebugILFrame corDebugFrame, CordbThread thread, CordbManagedModule module, CrossPlatformContext context) : base(corDebugFrame, thread, module, context)
        {
            Function = new CordbILFunction(corDebugFrame.Function, module);
        }

        /// <summary>
        /// Gets the arguments and local variables contained in this frame.
        /// </summary>
        public override CordbVariable[] Variables
        {
            get
            {
                //Each CordbVariableHome represents either an argument passed to a function, or a local variable declared within the function. This is determined
                //by the m_isLocal member. When a variable is an argument, you should ask for the ArgumentIndex. Otherwise, you should ask for the SlotIndex.

                var nativeCode = CorDebugFrame.Function.NativeCode;

                var homes = nativeCode.VariableHomes;

                var ip = CorDebugFrame.IP.pnOffset;

                var variables = new List<CordbVariable>();
                var variableSymbols = Module.SymbolModule.EnumerateManagedVariables(Function.CorDebugFunction.Token, ip).ToArray();

                var parameterSymbolMap = new Dictionary<int, ManagedParameterSymbol>();
                var localSymbolMap = new Dictionary<int, ManagedLocalSymbol>();

                foreach (var symbol in variableSymbols)
                {
                    if (symbol is ManagedParameterSymbol p)
                        parameterSymbolMap.Add(symbol.Index, p);
                    else
                        localSymbolMap.Add(symbol.Index, (ManagedLocalSymbol) symbol);
                }

                //Each variable knows its relative offset within memory. In order to get its absolute address, we need to take into consideration
                //which code chunk its in

                var chunks = nativeCode.CodeChunks;

                if (chunks.Length > 1)
                    throw new System.NotImplementedException("Calculating the addresses of variables split across multiple code chunks is not implemented.");

                var startAddr = chunks.Single().startAddr;

                //Homes describe the various locations that a given variable might reside at within a given method. A variable
                //may live at a variety of different homes across the life of the method, however at any given position in the method
                //you would expect there to be exactly one home that a given variable belongs to.
                for (var i = 0; i < homes.Length; i++)
                {
                    var home = homes[i];

                    //If the current IL IP is not within the live ranges of the specified home, we're not within scope of that home and it doesn't apply
                    //(i.e. it might be inside a different block scope or something)

                    var homeScope = home.LiveRange;

                    if (ip >= homeScope.pStartOffset && ip <= homeScope.pEndOffset)
                    {
                        //The home is live at the current IP

                        CordbManagedVariable variable;

                        if (home.TryGetArgumentIndex(out var parameterIndex) == HRESULT.S_OK)
                        {
                            var symbol = parameterSymbolMap[parameterIndex];

                            variable = new CordbManagedParameterVariable(
                                home,
                                symbol,
                                this,
                                Module,
                                homeScope.pStartOffset + startAddr,
                                homeScope.pEndOffset + startAddr
                            );
                        }
                        else
                        {
                            var localIndex = home.SlotIndex;

                            //Homes also capture temporaries that don't explicitly get assigned to variables. For example, the code "if (a > b)"
                            //creates a temporary in IL that stores the result of a > b. We don't care about representing these

                            if (!localSymbolMap.TryGetValue(localIndex, out var symbol))
                                continue; //It's a temporary; ignore

                            variable = new CordbManagedLocalVariable(
                                home,
                                symbol,
                                this,
                                Module,
                                homeScope.pStartOffset + startAddr,
                                homeScope.pEndOffset + startAddr
                            );
                        }

                        variables.Add(variable);
                    }
                }

                return variables.ToArray();
            }
        }
    }
}
