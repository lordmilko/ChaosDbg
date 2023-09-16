using System.Windows.Controls;

namespace ChaosDbg
{
    /// <summary>
    /// Represents an item that is contained in a <see cref="SplitterItemsControl"/>.<para/>
    /// This control should not be used directly; items stored in the <see cref="SplitterItemsControl"/> will
    /// automatically be wrapped inside an instance of this type.
    /// </summary>
    public partial class SplitterItem : ContentControl
    {
        public SplitterItem()
        {
            InitializeComponent();
        }
    }
}
