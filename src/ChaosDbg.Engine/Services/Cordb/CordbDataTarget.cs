using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ChaosLib;
using ClrDebug;
using static ClrDebug.HRESULT;

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Provides facilities for interacting with the memory of a target process.
    /// </summary>
    public class CordbDataTarget : ICLRDataTarget, ICorDebugDataTarget, IMemoryReader, IMemoryWriter
    {
        //IXCLRDataProcess::Flush does not use DAC_ENTER, which can lead to heap corruption if the process is flushed while it is running. To work around this,
        //ReadVirtual is tricked to call IXCLRDataProcess::Flush upon attempting to execute another method (which DOES call DAC_ENTER), thereby giving us a safe flush.
        //Taken from RuntimeBuilder.FlushDac() in ClrMD.
        private Action doFlush;
        private volatile int flushContext;
        private ulong MagicFlushAddress = 0x43; //A random (invalid) address constant to signal we're doing a flush, not reading an address

        private int pid;
        private LiveProcessMemoryReader reader;

        public CordbDataTarget(IntPtr hProcess)
        {
            pid = Kernel32.GetProcessId(hProcess);
            reader = new LiveProcessMemoryReader(hProcess);
        }

        public void SetFlushCallback(Action action)
        {
            doFlush = action;
        }

        /// <summary>
        /// Invokes <see cref="XCLRDataProcess.Flush"/> inside a DAC_ENTER lock.
        /// </summary>
        /// <param name="sos">The <see cref="SOSDacInterface"/> instance to use to trick the DAC into entering a DAC_ENTER lock.</param>
        public void Flush(SOSDacInterface sos)
        {
            Interlocked.Increment(ref flushContext);

            try
            {
                //GetWorkRequestData() is simply a random method that is safe to invoke with our MagicFlushAddress value.
                //It will call DAC_ENTER(), issue a ReadVirtual(), we'll perform the real flush, return E_FAIL and then
                //it will DAC_LEAVE(). 
                sos.TryGetWorkRequestData(MagicFlushAddress, out _);
            }
            finally
            {
                Interlocked.Decrement(ref flushContext);
            }
        }

        #region ICLRDataTarget

        HRESULT ICLRDataTarget.GetMachineType(out IMAGE_FILE_MACHINE machineType)
        {
            machineType = reader.Is32Bit ? IMAGE_FILE_MACHINE.I386 : IMAGE_FILE_MACHINE.AMD64;
            return S_OK;
        }

        HRESULT ICLRDataTarget.GetPointerSize(out int pointerSize)
        {
            pointerSize = reader.PointerSize;
            return S_OK;
        }

        HRESULT ICLRDataTarget.GetImageBase(string imagePath, out CLRDATA_ADDRESS baseAddress)
        {
            //In .NET Core it's possible to dbgshim!RegisterForRuntimeStartup (with the catch-22 you already need to know
            //where your dbgshim.dll is). In .NET Framework however there is no such function, so our fallback strategy is to repeatedly poll to see when it's loaded

            ProcessModule module = null;

            for (var i = 0; i < 100; i++)
            {
                //Once Process.Modules have been retrieved, they'll be cached. As such we re-retrieve the Process each time to get the latest available set of modules
                module = Process.GetProcessById(pid).Modules.Cast<ProcessModule>().FirstOrDefault(m => StringComparer.OrdinalIgnoreCase.Equals(m.ModuleName, imagePath));

                if (module != null)
                    break;

                Thread.Sleep(50);
            }

            if (module == null)
            {
                baseAddress = 0;
                return E_FAIL;
            }

            baseAddress = module.BaseAddress.ToInt64();
            return S_OK;
        }

        unsafe HRESULT ICLRDataTarget.ReadVirtual(CLRDATA_ADDRESS address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            if (address == MagicFlushAddress && flushContext > 0)
            {
                doFlush?.Invoke();
                bytesRead = 0;
                return E_FAIL;
            }

            return reader.ReadVirtual(address, buffer, bytesRequested, out bytesRead);
        }

        #region Not Implemented

        HRESULT ICLRDataTarget.WriteVirtual(CLRDATA_ADDRESS address, IntPtr buffer, int bytesRequested, out int bytesWritten)
        {
            var result = Kernel32.WriteProcessMemory(reader.hProcess, address, buffer, bytesRequested, out bytesWritten);

            if (!result)
                return (HRESULT) Marshal.GetHRForLastWin32Error();

            return S_OK;
        }

        HRESULT ICLRDataTarget.GetTLSValue(int threadID, int index, out CLRDATA_ADDRESS value) =>
            throw new NotImplementedException();

        HRESULT ICLRDataTarget.SetTLSValue(int threadID, int index, CLRDATA_ADDRESS value) =>
            throw new NotImplementedException();

        HRESULT ICLRDataTarget.GetCurrentThreadID(out int threadID) =>
            throw new NotImplementedException();

        unsafe HRESULT ICLRDataTarget.GetThreadContext(int threadID, ContextFlags contextFlags, int contextSize, IntPtr context)
        {
            HRESULT hr;

            if (context == IntPtr.Zero)
                return E_INVALIDARG;

            if ((hr = Kernel32.TryOpenThread(ThreadAccess.GET_CONTEXT, false, threadID, out var hThread)) != S_OK)
                return hr;

            if (contextFlags >= ContextFlags.AMD64Context && contextFlags <= ContextFlags.AMD64ContextAll)
            {
                if (contextSize < Marshal.SizeOf<AMD64_CONTEXT>())
                    return E_INVALIDARG;

                ((AMD64_CONTEXT*) context)->ContextFlags = contextFlags;
            }
            else
            {
                //Some type of x86 context
                Marshal.WriteInt32(context, (int) contextFlags);
            }

            try
            {
                hr = Kernel32.TryGetThreadContext(hThread, context);

                return hr;
            }
            finally
            {
                hThread.Dispose();
            }
        }

        HRESULT ICLRDataTarget.SetThreadContext(int threadID, int contextSize, IntPtr context)
        {
            HRESULT hr;

            if (context == IntPtr.Zero)
                return E_INVALIDARG;

            if ((hr = Kernel32.TryOpenThread(ThreadAccess.SET_CONTEXT, false, threadID, out var hThread)) != S_OK)
                return hr;

            try
            {
                hr = Kernel32.TrySetThreadContext(hThread, context);

                return hr;
            }
            finally
            {
                hThread.Dispose();
            }
        }

        HRESULT ICLRDataTarget.Request(uint reqCode, int inBufferSize, IntPtr inBuffer, int outBufferSize, IntPtr outBuffer) =>
            throw new NotImplementedException();

        #endregion
        #endregion
        #region ICorDebugDataTarget

        private ICLRDataTarget LegacyTarget => this;

        HRESULT ICorDebugDataTarget.GetPlatform(out CorDebugPlatform pTargetPlatform)
        {
            pTargetPlatform = default;

            var hr = LegacyTarget.GetMachineType(out var machineType);

            if (hr != S_OK)
                return hr;

            switch (machineType)
            {
                case IMAGE_FILE_MACHINE.I386:
                    pTargetPlatform = CorDebugPlatform.CORDB_PLATFORM_WINDOWS_X86;
                    break;

                case IMAGE_FILE_MACHINE.AMD64:
                    pTargetPlatform = CorDebugPlatform.CORDB_PLATFORM_WINDOWS_AMD64;
                    break;

                case IMAGE_FILE_MACHINE.IA64:
                    pTargetPlatform = CorDebugPlatform.CORDB_PLATFORM_WINDOWS_IA64;
                    break;

                case IMAGE_FILE_MACHINE.ARMNT:
                    pTargetPlatform = CorDebugPlatform.CORDB_PLATFORM_WINDOWS_ARM;
                    break;

                case IMAGE_FILE_MACHINE.ARM64:
                    pTargetPlatform = CorDebugPlatform.CORDB_PLATFORM_WINDOWS_ARM64;
                    break;

                default:
                    return E_NOTIMPL;
            }

            return S_OK;
        }

        HRESULT ICorDebugDataTarget.ReadVirtual(CORDB_ADDRESS address, IntPtr pBuffer, int bytesRequested, out int pBytesRead) =>
            LegacyTarget.ReadVirtual(address, pBuffer, bytesRequested, out pBytesRead);

        HRESULT ICorDebugDataTarget.GetThreadContext(int dwThreadId, ContextFlags contextFlags, int contextSize, IntPtr pContext) =>
            LegacyTarget.GetThreadContext(dwThreadId, contextFlags, contextSize, pContext);

        #endregion
        #region IMemoryReader

        bool IMemoryReader.Is32Bit => reader.Is32Bit;

        int IMemoryReader.PointerSize => reader.PointerSize;

        HRESULT IMemoryReader.ReadVirtual(long address, IntPtr buffer, int bytesRequested, out int bytesRead) =>
            reader.ReadVirtual(address, buffer, bytesRequested, out bytesRead);

        #endregion
    }
}
