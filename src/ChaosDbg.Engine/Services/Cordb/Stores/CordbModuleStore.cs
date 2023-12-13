using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;

namespace ChaosDbg.Cordb
{
    public class CordbModuleStore : IEnumerable<ICordbModule>
    {
        private object moduleLock = new object();

        private Dictionary<CORDB_ADDRESS, CordbManagedModule> managedModules = new Dictionary<CORDB_ADDRESS, CordbManagedModule>();
        private Dictionary<CORDB_ADDRESS, CordbNativeModule> nativeModules = new Dictionary<CORDB_ADDRESS, CordbNativeModule>();

        private CordbProcess process;
        private CordbEngineServices services;

        public CordbModuleStore(CordbProcess process, CordbEngineServices services)
        {
            this.process = process;
            this.services = services;
        }

        internal ICordbModule Add(CorDebugModule corDebugModule)
        {
            lock (moduleLock)
            {
                var module = new CordbManagedModule(corDebugModule);

                managedModules.Add(corDebugModule.BaseAddress, module);

                if (process.Session.IsInterop)
                {
                    if (nativeModules.TryGetValue(corDebugModule.BaseAddress, out var native))
                    {
                        if (native.ManagedModule != null)
                            throw new InvalidOperationException($"Cannot set managed module: native module '{native}' already has a native module associated with it.");

                        native.ManagedModule = module;
                        module.NativeModule = native;
                    }
                }

                return module;
            }
        }

        internal unsafe CordbNativeModule Add(in LOAD_DLL_DEBUG_INFO loadDll)
        {
            var baseAddress = (long) (void*) loadDll.lpBaseOfDll;

            var name = GetNativeModuleName(loadDll.lpImageName, loadDll.fUnicode == 1);

            var stream = CordbMemoryStream.CreateRelative(process.DAC.DataTarget, baseAddress);
            var peFile = services.PEFileProvider.ReadStream(stream, true);

            lock (moduleLock)
            {
                //The only way to get the image size is to read the PE header;
                //DbgEng does that too

                var native = new CordbNativeModule(baseAddress, name, peFile.OptionalHeader.SizeOfImage);

                nativeModules.Add(baseAddress, native);

                if (managedModules.TryGetValue(baseAddress, out var managed))
                {
                    if (managed.NativeModule != null)
                        throw new InvalidOperationException($"Cannot set native module: managed module '{managed}' already has a native module associated with it.");

                    native.ManagedModule = managed;
                    managed.NativeModule = native;
                }

                return native;
            }
        }

        private unsafe string GetNativeModuleName(IntPtr lpImageName, bool isUnicode)
        {
            //lpImageName MAY point to an address in the target process, or may be null.
            //The address that is pointed to MAY then be null itself, or may point to a valid string

            if (lpImageName == IntPtr.Zero)
                return null;

            var ptrSize = process.Is32Bit ? 4 : 8;
            var maxPathSize = isUnicode ? 522 : 261; //(MAX_PATH (260) + 1) * 2

            using var strPtrBuffer = new MemoryBuffer(ptrSize);
            using var strBuffer = new MemoryBuffer(maxPathSize);

            MemoryReader memoryReader = process.DAC.DataTarget;

            var hr = memoryReader.ReadVirtual((long) (void*) lpImageName, strPtrBuffer, ptrSize, out _);

            if (hr != HRESULT.S_OK)
                return null;

            //On the off chance that we're a 32-bit process debugging a 64-bit process for some insane reason, marshalling to IntPtr will truncate the address.
            //Straightup marshalling to long might not be a good idea, since that could read 8 bytes instead of 4. For safety, we'll marshal differently based on the pointer size
            //we're after

            long strPtr;

            if (ptrSize == 4)
                strPtr = Marshal.PtrToStructure<int>(strPtrBuffer);
            else
                strPtr = Marshal.PtrToStructure<long>(strPtrBuffer);

            if (strPtr == 0)
                return null;

            hr = memoryReader.ReadVirtual(strPtr, strBuffer, maxPathSize, out var read);

            if (hr != HRESULT.S_OK || read != maxPathSize)
                return null;

            string str;

            if (isUnicode)
                str = Marshal.PtrToStringUni(strBuffer);
            else
                str = Marshal.PtrToStringAnsi(strBuffer);

            return str;
        }

        internal CordbManagedModule Remove(CORDB_ADDRESS baseAddress)
        {
            lock (moduleLock)
            {
                if (managedModules.TryGetValue(baseAddress, out var module))
                {
                    managedModules.Remove(module.BaseAddress);

                    if (process.Session.IsInterop)
                    {
                        if (nativeModules.TryGetValue(baseAddress, out var native))
                        {
                            native.ManagedModule = null;
                            module.NativeModule = null;
                        }
                    }
                }

                return module;
            }
        }

        internal CordbNativeModule Remove(in UNLOAD_DLL_DEBUG_INFO unloadDll)
        {
            lock (moduleLock)
            {
                if (nativeModules.TryGetValue(unloadDll.lpBaseOfDll, out var native))
                {
                    nativeModules.Remove(unloadDll.lpBaseOfDll);

                    if (managedModules.TryGetValue(unloadDll.lpBaseOfDll, out var managed))
                    {
                        managed.NativeModule = null;
                        native.ManagedModule = null;
                    }
                }

                return native;
            }
        }

        public IEnumerator<ICordbModule> GetEnumerator()
        {
            lock (moduleLock)
            {
                return managedModules.Values
                    .Cast<ICordbModule>()
                    .Concat(nativeModules.Values.Cast<ICordbModule>())
                    .ToList()
                    .GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
