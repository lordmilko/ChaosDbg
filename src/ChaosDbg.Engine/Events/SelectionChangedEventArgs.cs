using System;
using ChaosDbg.Text;

namespace ChaosDbg
{
    public class SelectionChangedEventArgs : EventArgs
    {
        public TextRange SelectedRange { get; }

        public string SelectedText { get; }

        public SelectionChangedEventArgs(TextRange selectedRange, string selectedText)
        {
            SelectedRange = selectedRange;
            SelectedText = selectedText;
        }        
    }
}
