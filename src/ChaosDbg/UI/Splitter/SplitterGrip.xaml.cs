using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ChaosDbg
{
    /// <summary>
    /// Interaction logic for SplitterGrip.xaml
    /// </summary>
    [DependencyProperty("Orientation", typeof(Orientation), DefaultValue = Orientation.Vertical)]
    [DependencyProperty("ResizeBehavior", typeof(GridResizeBehavior), DefaultValue = GridResizeBehavior.CurrentAndNext)]
    public partial class SplitterGrip : Thumb
    {
        public SplitterGrip()
        {
            InitializeComponent();
        }
    }
}
