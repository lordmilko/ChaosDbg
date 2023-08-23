using System;
using System.Collections.Generic;
using System.IO;

namespace ChaosDbg.Disasm
{
    /// <summary>
    /// Represents a stream that is used to read disassembly information from a target.
    /// </summary>
    public sealed class DisasmStream : RelayStream
    {
        //We want to be able to display the bytes that corresponded to each CPU instruction. Iced doesn't store this
        //information, its assumed the caller already has it. All our information comes from a stream.
        //Read() may be called over multiple calls. We want to record all of the bytes that were read across
        //all Read() calls that were required in order to read the instruction.
        private List<byte> readBytes = new List<byte>();

        public DisasmStream(Stream stream) : base(stream)
        {
        }

        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            //Read some of the bytes required for the instruction
            var result = Stream.Read(buffer, offset, count);

            //If any bytes were read, cache them for when we need them
            if (result > 0)
            {
                for (var i = offset; i < result; i++)
                    readBytes.Add(buffer[i]);
            }

            return result;
        }

        public void ClearReadBytes() => readBytes.Clear();

        public byte[] ExtractReadBytes()
        {
            if (readBytes.Count == 0)
                return Array.Empty<byte>();

            var result = readBytes.ToArray();
            readBytes.Clear();
            return result;
        }
    }
}
