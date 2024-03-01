using System;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Tests
{
    class MockCorDebugFunction : ICorDebugFunction
    {
        public HRESULT GetModule(out ICorDebugModule ppModule)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetClass(out ICorDebugClass ppClass)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetToken(out mdMethodDef pMethodDef)
        {
            pMethodDef = (mdMethodDef) Extensions.TokenFromRid(1, CorTokenType.mdtMethodDef);
            return S_OK;
        }

        public MockCorDebugCode GetILCode { get; set; }

        HRESULT ICorDebugFunction.GetILCode(out ICorDebugCode ppCode)
        {
            if (GetILCode == null)
            {
                ppCode = default;
                return E_FAIL;
            }

            ppCode = GetILCode;
            return S_OK;
        }

        public MockCorDebugCode GetNativeCode { get; set; }

        HRESULT ICorDebugFunction.GetNativeCode(out ICorDebugCode ppCode)
        {
            if (GetNativeCode == null)
            {
                ppCode = default;
                return E_FAIL;
            }

            ppCode = GetNativeCode;
            return S_OK;
        }

        public HRESULT CreateBreakpoint(out ICorDebugFunctionBreakpoint ppBreakpoint)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetLocalVarSigToken(out mdSignature pmdSig)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetCurrentVersionNumber(out int pnCurrentVersion)
        {
            throw new NotImplementedException();
        }
    }
}
