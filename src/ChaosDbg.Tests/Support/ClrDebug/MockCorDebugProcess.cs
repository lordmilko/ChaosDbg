using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Tests
{
    class MockCorDebugProcess : MockCorDebugController, ICorDebugProcess
    {
        private Process process = Process.GetCurrentProcess();

        public HRESULT GetID(out int pdwProcessId)
        {
            pdwProcessId = process.Id;
            return S_OK;
        }

        public HRESULT GetHandle(out IntPtr phProcessHandle)
        {
            phProcessHandle = process.Handle;
            return S_OK;
        }

        public HRESULT GetThread(int dwThreadId, out ICorDebugThread ppThread)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumerateObjects(out ICorDebugObjectEnum ppObjects)
        {
            throw new NotImplementedException();
        }

        public HRESULT IsTransitionStub(CORDB_ADDRESS address, out bool pbTransitionStub)
        {
            throw new NotImplementedException();
        }

        public HRESULT IsOSSuspended(int threadID, out bool pbSuspended)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetThreadContext(int threadID, int contextSize, IntPtr context)
        {
            throw new NotImplementedException();
        }

        public HRESULT SetThreadContext(int threadID, int contextSize, IntPtr context)
        {
            throw new NotImplementedException();
        }

        public Dictionary<CORDB_ADDRESS, byte[]> ReadMemory { get; set; }

        HRESULT ICorDebugProcess.ReadMemory(CORDB_ADDRESS address, int size, IntPtr buffer, out int read)
        {
            if (ReadMemory.TryGetValue(address, out var bytes))
            {
                if (bytes.Length != size)
                    throw new NotImplementedException();

                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                read = bytes.Length;
                return S_OK;
            }

            throw new NotImplementedException();
        }

        public HRESULT WriteMemory(CORDB_ADDRESS address, int size, IntPtr buffer, out int written)
        {
            throw new NotImplementedException();
        }

        public HRESULT ClearCurrentException(int threadID)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnableLogMessages(bool fOnOff)
        {
            throw new NotImplementedException();
        }

        public HRESULT ModifyLogSwitch(string pLogSwitchName, int lLevel)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumerateAppDomains(out ICorDebugAppDomainEnum ppAppDomains)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetObject(out ICorDebugValue ppObject)
        {
            throw new NotImplementedException();
        }

        public HRESULT ThreadForFiberCookie(int fiberCookie, out ICorDebugThread ppThread)
        {
            throw new NotImplementedException();
        }

        public HRESULT GetHelperThreadID(out int pThreadID)
        {
            throw new NotImplementedException();
        }
    }
}
