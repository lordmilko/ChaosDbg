using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Tests
{
    class MockCorDebugCode : ICorDebugCode, ICorDebugCode2
    {
        #region ICorDebugCode

        public HRESULT IsIL(out bool pbIL)
        {
            throw new System.NotImplementedException();
        }

        public HRESULT GetFunction(out ICorDebugFunction ppFunction)
        {
            throw new System.NotImplementedException();
        }

        public HRESULT GetAddress(out CORDB_ADDRESS pStart)
        {
            throw new System.NotImplementedException();
        }

        public HRESULT GetSize(out int pcBytes)
        {
            if (GetCode == null)
            {
                pcBytes = default;
                return E_FAIL;
            }

            pcBytes = GetCode.Length;
            return S_OK;
        }

        public HRESULT CreateBreakpoint(int offset, out ICorDebugFunctionBreakpoint ppBreakpoint)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetCode { get; set; }

        HRESULT ICorDebugCode.GetCode(int startOffset, int endOffset, int cBufferAlloc, byte[] buffer, out int pcBufferSize)
        {
            if (GetCode == null)
            {
                pcBufferSize = default;
                return E_FAIL;
            }

            pcBufferSize = GetCode.Length;

            if (buffer == null)
                return S_FALSE;

            for (var i = 0; i < GetCode.Length; i++)
                buffer[i] = GetCode[i];

            return S_OK;
        }

        public HRESULT GetVersionNumber(out int nVersion)
        {
            throw new System.NotImplementedException();
        }

        public COR_DEBUG_IL_TO_NATIVE_MAP[] GetILToNativeMapping { get; set; }

        HRESULT ICorDebugCode.GetILToNativeMapping(int cMap, out int pcMap, COR_DEBUG_IL_TO_NATIVE_MAP[] map)
        {
            if (GetILToNativeMapping == null)
            {
                pcMap = default;
                return E_FAIL;
            }

            pcMap = GetILToNativeMapping.Length;

            if (map == null)
                return S_FALSE;

            for (var i = 0; i < GetILToNativeMapping.Length; i++)
                map[i] = GetILToNativeMapping[i];

            return S_OK;
        }

        public HRESULT GetEnCRemapSequencePoints(int cMap, out int pcMap, int[] offsets)
        {
            throw new System.NotImplementedException();
        }

        #endregion
        #region ICorDebugCode2

        public CodeChunkInfo[] GetCodeChunks { get; set; }

        HRESULT ICorDebugCode2.GetCodeChunks(int cbufSize, out int pcnumChunks, CodeChunkInfo[] chunks)
        {
            if (GetCodeChunks == null)
            {
                pcnumChunks = default;
                return E_FAIL;
            }

            pcnumChunks = GetCodeChunks.Length;

            if (chunks == null)
                return S_FALSE;

            for (var i = 0; i < GetCodeChunks.Length; i++)
                chunks[i] = GetCodeChunks[i];

            return S_OK;
        }

        public HRESULT GetCompilerFlags(out CorDebugJITCompilerFlags pdwFlags)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
