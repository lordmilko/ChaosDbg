using System;
using ChaosLib;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    class ConsoleTtdErrorReporting : ErrorReporting
    {
        public override void PrintError(string error)
        {
            Console.WriteLine(error);
        }

        public override void VPrintError(string format, IntPtr vaList)
        {
            var str = StringExtensions.printf(format, vaList);

            Console.WriteLine(str);
        }
    }
}
