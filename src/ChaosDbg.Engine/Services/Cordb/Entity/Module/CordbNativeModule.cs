using System;
using System.IO;
using System.Runtime.InteropServices;
using ChaosLib;
using ChaosLib.Handle;
using ChaosLib.PortableExecutable;
using ChaosLib.Symbols;
using ClrDebug;

#nullable enable

namespace ChaosDbg.Cordb
{
    /// <summary>
    /// Represents a native module in an interop debugging session.
    /// </summary>
    public class CordbNativeModule : CordbModule
    {
        public static bool IsCLRName(string fileName) => StringComparer.OrdinalIgnoreCase.Equals(fileName, "coreclr.dll") || StringComparer.OrdinalIgnoreCase.Equals(fileName, "clr.dll");

        /// <summary>
        /// Gets whether this is the module representing the CLR.
        /// </summary>
        public bool IsCLR { get; }

        private IUnmanagedSymbolModule? symbolModule;

        /// <summary>
        /// Provides access to any symbol information that is available for this module.
        /// </summary>
        public IUnmanagedSymbolModule? SymbolModule
        {
            get
            {
                /* Even if we don't have any symbols, we still must inform DbgHelp about this module. SymFunctionTableAccess64
                 * calls dbghelp!GetModuleForPC internally, which means if DbgHelp doesn't know about this module, on x64 we won't
                 * be able to unwind through it! It's very important we use SYMOPT.DEFERRED_LOADS (set in CordbProcess.ctor()), otherwise
                 * this can cause a half a second delay while we search for symbols */
                if (symbolModule == null && peFile != null)
                    symbolModule = Process.Symbols.GetOrAddNativeModule(Name, BaseAddress, peFile.OptionalHeader.SizeOfImage);

                return symbolModule;
            }
        }

        /// <summary>
        /// Gets the managed counterpart of this native module. This value may be null if this module has no managed counterpart,
        /// or the debugger has not yet received the managed load event for this module yet.<para/>
        /// If this module is the native counterpart of a <see cref="CordbManagedProcessPseudoModule"/>, there will be no reference back to the managed counterpart.
        /// </summary>
        public CordbManagedModule? ManagedModule { get; set; }

        public CordbNativeModule(
            string name,
            long baseAddress,
            CordbProcess process,
            PEFile peFile) : base(name, baseAddress, peFile.OptionalHeader.SizeOfImage, process, peFile)
        {
            var fileName = Path.GetFileName(name);

            IsCLR = IsCLRName(fileName);
        }

        /// <summary>
        /// Gets the PE File of this module without forcing symbol resolution.
        /// </summary>
        /// <returns>The PE File of this module.</returns>
        internal PEFile? GetRawPEFile() => peFile;

        #region GetNativeModuleName

        internal static string GetNativeModuleName(in LOAD_DLL_DEBUG_INFO loadDll, int processId)
        {
            /* DbgEng does a number of fancy tricks to try and extract the module name out of the remote process: from reading
             * the provided lpImageName, various sections of the in memory or on disk PE File, and even from the module list
             * specified in the PEB. Even after all this work, the information it comes back with can still be faulty:
             *
             * - the file name you retrieve might not contain the directory path (resulting in a call to the PEB anyway to get this info...
             *   unless you're ntdll or the exe itself, in which case the PEB isn't initialized yet)
             * - or even worse: reading the PE File would tell you that pwsh.exe is still called apphost.exe. Wrong!
             *
             * Visual Studio correctly identifies that pwsh.exe is not still called apphost.exe. Considering that we've been
             * given a handle to the module's file, why don't we just use it to lookup the name of the file? */

            var fileHandle = HandleInfo.New<FileHandleInfo>(loadDll.hFile, processId);

            return fileHandle.Name;
        }

#if DEBUG
        //This method demonstrates the supposed "proper" way of getting a module name that DbgEng uses.
        //As stated above however, if we can get the name from the hFile, why not just do that?
        private static string AlternateModuleNameStrategies(
            MemoryReader memoryReader,
            PEFile peFile,
            in LOAD_DLL_DEBUG_INFO loadDll)
        {
            var name = GetNativeModuleNameFromPtr(memoryReader, loadDll.lpImageName, loadDll.fUnicode == 1);

            if (name == null)
                name = GetNativeModuleNameFromPE(peFile, loadDll.hFile);

            if (name == null)
                throw new NotImplementedException("Try get the module name from the PEB module list instead?");

            if (Path.GetPathRoot(name) == string.Empty)
            {
                //Need to get the absolute path to the module.
                //Note: you cannot query the PEB until both the exe itself and ntdll have been loaded. As such, for those two modules
                //it is not possible to resolve their full paths via the PEB

                var info = Ntdll.NtQueryInformationProcess<PROCESS_BASIC_INFORMATION>(memoryReader.hProcess, PROCESSINFOCLASS.ProcessBasicInformation);

                var peb = new RemotePeb(info.PebBaseAddress, memoryReader);

                throw new NotImplementedException("Don't know how to get module path from PEB module list");
            }

            return name;
        }

        private static unsafe string? GetNativeModuleNameFromPtr(MemoryReader memoryReader, IntPtr lpImageName, bool isUnicode)
        {
            //The lpImageName apparently points to TEB.NT_TIB.ArbitraryUserPointer and is a giant hack that
            //Windows uses to relay the module name to debuggers.

            //lpImageName MAY point to an address in the target process, or may be null.
            //The address that is pointed to MAY then be null itself, or may point to a valid string

            if (lpImageName == IntPtr.Zero)
                return null;

            var maxPathSize = isUnicode ? 522 : 261; //(MAX_PATH (260) + 1) * 2

            using var strPtrBuffer = new MemoryBuffer(memoryReader.PointerSize);
            using var strBuffer = new MemoryBuffer(maxPathSize);

            var hr = memoryReader.ReadVirtual((long) (void*) lpImageName, strPtrBuffer, memoryReader.PointerSize, out _);

            if (hr != HRESULT.S_OK)
                return null;

            //On the off chance that we're a 32-bit process debugging a 64-bit process for some insane reason, marshalling to IntPtr will truncate the address.
            //Straightup marshalling to long might not be a good idea, since that could read 8 bytes instead of 4. For safety, we'll marshal differently based on the pointer size
            //we're after

            long strPtr;

            if (memoryReader.PointerSize == 4)
                strPtr = Marshal.PtrToStructure<int>(strPtrBuffer);
            else
                strPtr = Marshal.PtrToStructure<long>(strPtrBuffer);

            if (strPtr == 0)
                return null;

            hr = memoryReader.ReadVirtual(strPtr, strBuffer, maxPathSize, out var read);

            if (hr != HRESULT.S_OK || read != maxPathSize)
                return null;

            string? str;

            if (isUnicode)
                str = Marshal.PtrToStringUni(strBuffer);
            else
                str = Marshal.PtrToStringAnsi(strBuffer);

            return str;
        }

        private static string? GetNativeModuleNameFromPE(PEFile peFile, IntPtr hFile)
        {
            //ntdll can hit this code path

            foreach (var dir in peFile.DebugDirectory.Entries)
            {
                if (dir.Type == ImageDebugType.Misc)
                    throw new NotImplementedException("Retrieving a module name from the Misc debug directory is not implemented.");
            }

            if (peFile.ExportDirectory != null)
            {
                if (peFile.ExportDirectory.Name != null)
                    return peFile.ExportDirectory.Name;
            }

            var codeViews = peFile.DebugDirectory.CodeView;

            if (codeViews != null && codeViews.Length > 0)
            {
                var fileName = Path.GetFileNameWithoutExtension(codeViews[0].Path);

                //Ostensibly we should have a filename ntdll.pdb. If the filename is ntdll.dll.pdb however,
                //fileName is going to still contain ".dll" in it
                var originalExt = Path.GetExtension(fileName).ToLower();

                if (originalExt != string.Empty && originalExt == ".exe" || originalExt == ".dll" || originalExt == ".sys")
                {
                    //Already have a filename, use as is then!
                    return fileName;
                }
                else if (peFile.FileHeader.Characteristics.HasFlag(ImageFile.Dll))
                    return fileName + ".dll";
                else
                    return fileName + ".exe";
            }

            return null;
        }
#endif

        #endregion
    }
}
