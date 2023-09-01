using System.Collections.Generic;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents an <see cref="IDebugOutputCallbacks"/> implementation that saves all emitted text to an in memory buffer.
    /// </summary>
    class BufferOutputCallbacks : IDebugOutputCallbacks
    {
        public List<string> Lines { get; } = new List<string>();

        /// <summary>
        /// Gets or sets whether the buffer is currently capturing emitted output.
        /// </summary>
        public bool Capturing { get; set; }

        public HRESULT Output(DEBUG_OUTPUT mask, string text)
        {
            if (Capturing)
                Lines.Add(text);

            return HRESULT.S_OK;
        }
    }
}
