using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using ChaosDbg.Cordb;
using ChaosLib;
using ClrDebug;
using static ClrDebug.Extensions;

namespace ChaosDbg.DAC
{
    /// <summary>
    /// Encapsulates facilities used for communicating with the DAC.
    /// </summary>
    public class DacProvider
    {
        public DacThreadStore Threads { get; }

        private SOSDacInterface sos;
        public SOSDacInterface SOS
        {
            get
            {
                if (sos == null)
                    sos = GetSOSDacInterface();

                return sos;
            }
        }

        /// <summary>
        /// Gets the data target that is used to communicate with the DAC.
        /// </summary>
        public CordbDataTarget DataTarget { get; }

        private CordbProcess cdbProcess;

        public DacProvider(CordbProcess process)
        {
            cdbProcess = process;

            DataTarget = new CordbDataTarget(process);
            DataTarget.SetFlushCallback(() => SOS.As<XCLRDataProcess>().Flush());
            Threads = new DacThreadStore(this);
        }

        public void Flush()
        {
            //GetWorkRequestData is not actually implemented in .NET Core. ISOSDacInterface13 is supported in .NET 8+.
            //Unless we find another method to get a DAC_ENTER with, we have a gap where most .NET Core versions won't
            //get locked when we do DataTarget.Flush(). This is what ClrMD does however

            if (SOS.Raw is ISOSDacInterface13)
                SOS.LockedFlush();
            else
                DataTarget.Flush(SOS);
        }

        #region Init

        private SOSDacInterface GetSOSDacInterface()
        {
            //Get a new Process so we get the latest list of modules. Modules won't refresh on a Process object after they've been
            //retrieved the first time
            var process = Process.GetProcessById(cdbProcess.Id);

            var modules = process.Modules.Cast<ProcessModule>().ToArray();

            var clr = modules.FirstOrDefault(m => m.ModuleName.Equals("clr.dll", StringComparison.OrdinalIgnoreCase));

            if (clr != null)
                return CLRDataCreateInstance(DataTarget).SOSDacInterface;

            var coreclr = modules.FirstOrDefault(m => m.ModuleName.Equals("coreclr.dll", StringComparison.OrdinalIgnoreCase));

            if (coreclr == null)
                throw new InvalidOperationException($"Could not find module clr.dll or coreclr.dll on process {cdbProcess.Id}.");

            return CoreCLRDataCreateInstance(coreclr).SOSDacInterface;
        }

        private CLRDataCreateInstanceInterfaces CoreCLRDataCreateInstance(ProcessModule module)
        {
            var dacPath = Path.Combine(Path.GetDirectoryName(module.FileName), "mscordaccore.dll");

            if (!File.Exists(dacPath))
                throw new FileNotFoundException($"Cannot find file '{dacPath}'.");

            var dacLib = Kernel32.LoadLibrary(dacPath);

            var clrDataCreateInstancePtr = Kernel32.GetProcAddress(dacLib, "CLRDataCreateInstance");

            var clrDataCreateInstanceDelegate = Marshal.GetDelegateForFunctionPointer<CLRDataCreateInstanceDelegate>(clrDataCreateInstancePtr);

            var clrDataCreateInstance = CLRDataCreateInstance(clrDataCreateInstanceDelegate, DataTarget);

            return clrDataCreateInstance;
        }

        #endregion
    }
}
