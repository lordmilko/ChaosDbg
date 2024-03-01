using System;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Tests
{
    class MockCorDebugModule : ICorDebugModule
    {
        public HRESULT GetProcess(out ICorDebugProcess ppProcess)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetBaseAddress(out CORDB_ADDRESS pAddress)
        {
            pAddress = 1;
            return S_OK;
        }

        public HRESULT GetAssembly(out ICorDebugAssembly ppAssembly)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetName(int cchName, out int pcchName, char[] szName)
        {
            pcchName = 2;

            if (szName == null)
                return S_FALSE;

            szName[0] = 'A';
            szName[1] = '\0';
            pcchName = 2;
            return S_OK;
        }

        public HRESULT EnableJITDebugging(bool bTrackJITInfo, bool bAllowJitOpts)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnableClassLoadCallbacks(bool bClassLoadCallbacks)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetFunctionFromToken(mdMethodDef methodDef, out ICorDebugFunction ppFunction)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetFunctionFromRVA(long rva, out ICorDebugFunction ppFunction)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetClassFromToken(mdTypeDef typeDef, out ICorDebugClass ppClass)
        {
            throw new NotImplementedException();
        }

        public HRESULT CreateBreakpoint(out ICorDebugModuleBreakpoint ppBreakpoint)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetEditAndContinueSnapshot(out ICorDebugEditAndContinueSnapshot ppEditAndContinueSnapshot)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetMetaDataInterface(Guid riid, out object ppObj)
        {
            if (riid == typeof(IMetaDataImport).GUID)
                ppObj = new MockMetaDataImport();
            else
                throw new NotImplementedException();

            return S_OK;
        }

        public HRESULT GetToken(out mdModule pToken)
        {
            throw new NotImplementedException();
        }

        public HRESULT IsDynamic(out int pDynamic)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetGlobalVariableValue(mdFieldDef fieldDef, out ICorDebugValue ppValue)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetSize(out int pcBytes)
        {
            pcBytes = 1;
            return S_OK;
        }

        public HRESULT IsInMemory(out int pInMemory)
        {
            throw new NotImplementedException();
        }
    }
}
