using System;
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
        private bool capturing;
        private List<string> lines = new List<string>();

        public HRESULT Output(DEBUG_OUTPUT mask, string text)
        {
            if (capturing)
                lines.Add(text);

            return HRESULT.S_OK;
        }

        public string[] Capture(Action action)
        {
            capturing = true;

            try
            {
                action();

                var output = string.Join(string.Empty, lines);

                return output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            }
            finally
            {
                capturing = false;
                lines.Clear();
            }
        }
    }
}
