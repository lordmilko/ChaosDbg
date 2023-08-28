using System;

namespace ChaosDbg
{
    public class EngineOutputEventArgs : EventArgs
    {
        public string Text { get; }

        public EngineOutputEventArgs(string text)
        {
            Text = text;
        }
    }
}
