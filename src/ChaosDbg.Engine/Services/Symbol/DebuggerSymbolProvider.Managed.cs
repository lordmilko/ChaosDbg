using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosLib;
using ChaosLib.Symbols;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Symbol
{
    public partial class DebuggerSymbolProvider
    {
        private Dictionary<long, SOSSymbolModule> loadedManagedModules = new Dictionary<long, SOSSymbolModule>();
        private Dictionary<long, IManagedSymbol> knownSymbols = new Dictionary<long, IManagedSymbol>();

        //Stores symbols retrieved from SOS that don't belong to a particular managed module (e.g. JIT Helpers, which are owned by the CLR)
        private SOSSymbolModule globalManagedModule = new SOSSymbolModule(0, "Global Symbols");

        private object loadedManagedModulesLock = new object();

        private SOSDacInterface sos;
        private XCLRDataProcess clrDataProcess;

        private ProcessModule clrModule;
        private IUnmanagedSymbolModule clrSymbolModule;

        private Thread eagerCLRSymbolsThread;

        private bool TryManagedSymFromAddr(long address, out IDisplacedSymbol result)
        {
            result = default;

            //Have we seen this address before?
            lock (loadedManagedModulesLock)
            {
                if (knownSymbols.TryGetValue(address, out var existing))
                {
                    result = AsDisplacedSymbol(existing);
                    return true;
                }
            }

            if (TryGetSOS())
            {
                if (TrySafeManagedSymFromAddr(address, out var symbol))
                {
                    result = AsDisplacedSymbol(symbol);
                    return true;
                }
            }

            return false;
        }

        private bool TrySafeManagedSymFromAddr(long address, out IManagedSymbol symbol)
        {
            //Only try methods that are guaranteed not to throw, as these can kill performance when attempting to resolve symbols
            //during disassembly

            if (TryGetJitHelperSymbol(address, out symbol))
                return true;

            /* While CLRDataAddressType may be a collection of flags, there are only really 3 possible
             * values that can be returned
             * - CLRDATA_ADDRESS_MANAGED_METHOD
             * - CLRDATA_ADDRESS_RUNTIME_UNMANAGED_STUB
             * - CLRDATA_ADDRESS_UNRECOGNIZED
             *
             * CLRDATA_ADDRESS_RUNTIME_UNMANAGED_CODE is unused, and CLRDATA_ADDRESS_RUNTIME_MANAGED_CODE / CLRDATA_ADDRESS_GC_DATA
             * are merely sub-flags of other flags.
             *
             * Not all managed methods/stubs are correctly identified by this method. It seems like DacpMethodDescData can catch
             * scenarios where an address might be an FCALL, which this method can't do
             */
            var addressType = clrDataProcess.GetAddressType(address);

            if (addressType == CLRDataAddressType.CLRDATA_ADDRESS_RUNTIME_UNMANAGED_STUB)
            {
                if (sos.TryGetCodeHeaderData(address, out var codeHeader) == S_OK)
                {
                    if (new DacpMethodDescData().Request(sos.Raw, codeHeader.MethodDescPtr) == S_OK)
                    {
                        if (sos.TryGetMethodDescName(codeHeader.MethodDescPtr, out var name) == S_OK)
                        {
                            //Sometimes the symbols retrieved from this are valid (e.g. DangerousAddRef) while other times it just spits out the name
                            //of the parent method and makes a lot of noise.

                            Debug.Assert(codeHeader.MethodStart == 0);
                            symbol = AddManagedMethod(address, name, 0, codeHeader);
                            return true;
                        }
                        else
                            Debug.Assert(false, "Does this throw on failure?");
                    }
                    else
                        Debug.Assert(false, "Shouldn't be calling this in this method if its going to throw");
                }
            }
            else
            {
                var hr = clrDataProcess.TryGetRuntimeNameByAddress(address, 0, out var result);

                switch (hr)
                {
                    case S_OK:
                    {
                        //We got a name, but now we have to figure out where to cache it!
                        switch (addressType)
                        {
                            case CLRDataAddressType.CLRDATA_ADDRESS_MANAGED_METHOD:
                                //We know it's a manged method, so requesting for a CodeHeader shouldn't fail
                                symbol = AddManagedMethod(
                                    address,
                                    result.nameBuf,
                                    result.displacement,
                                    sos.GetCodeHeaderData(address)
                                );
                                return true;

                            default:
                                throw new NotImplementedException($"Don't know how to handle {nameof(CLRDataAddressType)} '{addressType}'");
                        }
                    }

                    case E_INVALIDARG:
                        //ClrDataAccess::IsPossibleCodeAddress failed to read the memory address
                        break;

                    case E_NOINTERFACE:
                        //No matches
                        break;
                }
            }

            return false;
        }

        private bool TryGetJitHelperSymbol(long address, out IManagedSymbol result)
        {
            if (sos.TryGetJitHelperFunctionName(address, out var helperName) == S_OK)
            {
                TryNativeSymFromAddr(address, out var nativeSymbol);

                result = new SOSJitHelperSymbol(helperName, address, (IUnmanagedSymbol) nativeSymbol, nativeSymbol?.Module);

                //Regardless of whether we were able to resolve the symbol to a real native symbol or not, on the managed side it's part of the global symbol module
                AddManagedSymbol(globalManagedModule, result);

                return true;
            }

            result = default;
            return false;
        }

        private bool TryCLRSymFromAddr(long address, SymFromAddrOption options, out IDisplacedSymbol result)
        {
            //Force load the CLR if need be, but only query for symbols from it if we've not already planning to query native symbols after this
            if (TryRegisterCLR() && options.HasFlag(SymFromAddrOption.Native))
            {
                if (TryNativeSymFromAddr(address, out result))
                    return true;
            }

            result = default;
            return false;
        }

        private bool TryDangerousManagedSymFromAddr(long address, SymFromAddrOption options)
        {
            /* It's not clear to me under what circumstances we should ever try and do such dangerous
             * address resolves (e.g. under what circumstances would a MethodTable pointer be hanging out
             * in disassembly? It's not impossible inside the CLR, but outside it, it'd be quite rare.
             * So for now, this is disabled due to the fact it will kill performance */

#if FALSE
            if (options.HasFlag(SymFromAddrOption.Managed))
            {
                //If we trid safe managed symbols, then we already tried to load SOS. If it wasn't loaded, no point
                //trying again
                if (sos == null)
                    return false;
            }
            else
            {
                //Haven't tried loading SOS in the current symbol resolution attempt

                if (!TryGetSOS())
                    return false;
            }

            //No point trying to use GetMethodDescPtrFromIP. It does the exact same thing as GetCodeHeaderData,
            //except GetCodeHeaderData also tries to resolve MethodDescs from stubs, so is clearly the superior option

            //All of these methods may throw, hurting performance during symbol resolution, and should only be used as a last resort

            //If the address is for a MethodDesc, the IP needs to be resolved to the MethodDesc it actually pertains to. If this is a jump instruction, the address
            //could be part way into the method. It would seem that ultimately the IP is resolved to a MethodDesc via IJitManager::JitCodeToMethodInfo
            if (sos.TryGetCodeHeaderData(address, out var codeHeader) == S_OK)
            {
                var methodDesc = new DacpMethodDescData();

                if (methodDesc.Request(sos.Raw, codeHeader.MethodDescPtr) == S_OK)
                {
                    if (sos.TryGetMethodDescName(address, out var name) == S_OK)
                        throw new NotImplementedException();
                    else
                        throw new NotImplementedException(); //what is it?
                }
                else
                    throw new NotImplementedException(); //we've got a codeheader for it, so what is it?
            }
            else
            {
                //It's not a code address, but it still could be a MethodDesc

                var methodDesc = new DacpMethodDescData();

                if (methodDesc.Request(sos.Raw, address) == S_OK)
                {
                    if (sos.TryGetMethodDescName(address, out var name) == S_OK)
                        throw new NotImplementedException();
                    else
                        throw new NotImplementedException(); //what is it?
                }

                //Maybe a method table?
                var methodTable = new DacpMethodTableData();

                if (methodTable.Request(sos.Raw, address) == S_OK)
                {
                    if (sos.TryGetMethodTableName(address, out var name) == S_OK)
                        throw new NotImplementedException();
                    else
                        throw new NotImplementedException(); //what is it?
                }
            }
#endif

            return false;
        }

        private IManagedSymbol AddManagedMethod(CLRDATA_ADDRESS address, string name, CLRDATA_ADDRESS displacement, in DacpCodeHeaderData codeHeader)
        {
            //We know it's a manged method, so requesting for a MethodDesc shouldn't fail
            var methodDesc = new DacpMethodDescData();
            methodDesc.Request(sos.Raw, codeHeader.MethodDescPtr).ThrowOnNotOK();

            if (codeHeader.MethodStart != 0)
                Debug.Assert(codeHeader.MethodStart == address - displacement);

            SOSSymbolModule module;
            IManagedSymbol existingInner = null;

            lock (loadedManagedModulesLock)
            {
                if (!loadedManagedModules.TryGetValue(methodDesc.ModulePtr, out module))
                {
                    var clrDataModule = sos.GetModule(methodDesc.ModulePtr);

                    clrDataModule.TryGetFileName(out var moduleName);

                    module = new SOSSymbolModule(address, moduleName ?? "<No Name Available>");
                    loadedManagedModules[methodDesc.ModulePtr] = module;
                }

                if (codeHeader.MethodStart != 0)
                    knownSymbols.TryGetValue(codeHeader.MethodStart, out existingInner);
            }

            var toAdd = new List<IManagedSymbol>();

            IManagedSymbol resultSymbol;

            if (displacement == 0)
            {
                if (codeHeader.MethodStart == 0)
                {
                    //We skipped trying to find an existing symbol. We will declare that we are the real symbol
                    resultSymbol = new SOSSymbol(name, address, module);
                    toAdd.Add(resultSymbol);
                }
                else
                {
                    //If there's no displacement, we should have retrieved the inner symbol when we first asked at the very beginning of managed symbol processing
                    Debug.Assert(existingInner == null);

                    if (existingInner != null)
                        throw new InvalidOperationException($"Failed to retrieve existing symbol '{existingInner}' in an earlier code path");

                    //Our address is exactly the inner symbol
                    resultSymbol = new SOSSymbol(name, address, module);
                    toAdd.Add(resultSymbol);
                }
            }
            else
            {
                //There was some kind of displacement

                if (existingInner == null)
                {
                    //We should create the inner symbol at the same time
                    var start = codeHeader.MethodStart;

                    if (start == 0)
                        start = address;

                    existingInner = new SOSSymbol(name, start, module);
                    toAdd.Add(existingInner);
                }

                resultSymbol = new SOSDisplacedSymbol(displacement, existingInner);
                toAdd.Add(resultSymbol);
            }

            Debug.Assert(toAdd.Count > 0);
            AddManagedSymbol(module, toAdd.ToArray());

            return resultSymbol;
        }

        private void AddManagedSymbol(SOSSymbolModule module, params IManagedSymbol[] symbols)
        {
            lock (loadedManagedModulesLock)
            {
                foreach (var symbol in symbols)
                {
                    module.AddSymbol(symbol);
                    knownSymbols.Add(symbol.Address, symbol);
                }
            }
        }

        #region Add

        public void AddManagedModule(CLRDATA_ADDRESS address, string name)
        {
            lock (loadedManagedModulesLock)
            {
                loadedManagedModules[address] = new SOSSymbolModule(address, name);
            }
        }

        #endregion
        #region Remove

        public void RemoveManagedModule(CLRDATA_ADDRESS address)
        {
            lock (loadedManagedModulesLock)
            {
                if (loadedManagedModules.TryGetValue(address, out var module))
                {
                    loadedManagedModules.Remove(address);
                }
            }
        }

        #endregion

        private bool TryGetSOS()
        {
            if (sos != null)
                return true;

            if (extension == null)
                return false;

            if (extension.TryGetSOS(out sos, out clrModule))
            {
                clrDataProcess = sos.As<XCLRDataProcess>();
                return true;
            }

            return false;
        }

        private IDisplacedSymbol AsDisplacedSymbol(IManagedSymbol symbol)
        {
            if (symbol is IDisplacedSymbol d)
                return d;

            return new SOSDisplacedSymbol(0, (SOSSymbol) symbol);
        }

        internal void LoadCLRSymbols(int engineId, CancellationToken cancellationToken)
        {
            //When managed debugging, symbols for the CLR will not normally be loaded. However, it can often
            //be useful to have these when we invoke special CLR internal methods. Thus, we eagerly attempt
            //to load these so that they're available when we need them
            eagerCLRSymbolsThread = new Thread(() =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    TryRegisterCLR();
                }
                catch (Exception ex)
                {
                    Log.Error<IDbgHelp>(ex, "Failed to eagerly load CLR symbols: {message}", ex.Message);
                }
            });
            Log.CopyContextTo(eagerCLRSymbolsThread);
            eagerCLRSymbolsThread.Name = $"Eager CLR Symbols Thread {engineId}";
            eagerCLRSymbolsThread.Start();
        }

        //Only called when adding native modules through DbgHelp. We may get an IUnmanagedSymbolModule representation of managed modules as well
        private void TryRegisterCLR(IUnmanagedSymbolModule module)
        {
            //If we already know that the CLR has been registered with DbgHelp, no need to try and add it again
            if (clrSymbolModule != null)
                return;

            var name = Path.GetFileName(module.ModulePath);

            if (CordbNativeModule.IsCLRName(name))
                clrSymbolModule = module;
        }

        //Called when trying to resolve symbols in either interop mode or non-interop mode
        public unsafe bool TryRegisterCLR()
        {
            if (clrSymbolModule != null)
                return true;

            //If the CLR is loaded, but we haven't added it to DbgHelp yet, add it now
            if (TryGetSOS())
            {
                //Add it immediately now
                AddNativeModule(clrModule.FileName, (long) (void*) clrModule.BaseAddress, clrModule.ModuleMemorySize);

                return true;
            }
            else
            {
                //CLR is not loaded yet
                return false;
            }
        }
    }
}
