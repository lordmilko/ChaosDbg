using System;
using ChaosDbg.Cordb;
using ClrDebug;

namespace ChaosDbg
{
    class MemoryDumper
    {
        private CordbProcess process;
        private IConsole console;

        public MemoryDumper(CordbProcess process, IConsole console)
        {
            this.process = process;
            this.console = console;
        }

        public void Dump(long address, int count)
        {
            var size = 4; //dword

            //Want the raw memory, so don't try and read through mscordbi

            var hr = process.DataTarget.TryReadVirtual(address, 512, out var result);

            var bufferPos = 0;

            var numRows = count / size;

            for (var i = 0; i < numRows; i++)
            {
                console.Write("{0:x16} ", address);

                for (var j = 0; j < 4; j++) //4 cols
                {
                    var value = BitConverter.ToUInt32(result, bufferPos);

                    console.Write(" {0:x8}", value);

                    bufferPos += 4;
                }

                console.WriteLine();

                address += bufferPos;
            }
        }
    }
}
