using System.Windows;
using System.Windows.Controls;

namespace ChaosDbg
{
    //Everything you need to know about ItemsControl: http://drwpf.com/blog/itemscontrol-a-to-z/
    //In particular, articles "I" and "G"

    /// <summary>
    /// Represents a special type of <see cref="ItemsControl"/> (much like a <see cref="ListBox"/> or <see cref="ListView"/>)
    /// that automatically separates its children via the use of a resizable horizontal or vertical splitter.
    /// </summary>
    [DependencyProperty("Orientation", typeof(Orientation), DefaultValue = Orientation.Vertical, AffectsMeasure = true)]
    [AttachedDependencyProperty("SplitterGripSize", typeof(double), DefaultValue = 5.0, Inherits = true)]
    public partial class SplitterItemsControl : ItemsControl
    {
        public SplitterItemsControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets whether an object that has been stored in this items control is already our desired wrapper container type,
        /// and so should not be re-wrapped inside an instance returned from <see cref="GetContainerForItemOverride"/>.
        /// </summary>
        /// <param name="item">The object to inspect.</param>
        /// <returns>True if the object is already a wrapper container, otherwise false.</returns>
        protected override bool IsItemItsOwnContainerOverride(object item) => item is SplitterItem;

        /// <summary>
        /// Gets the control that should be used to wrap each item that is contained in this control.<para/>
        /// e.g. in a <see cref="ListView"/> this method returns a <see cref="ListViewItem"/>, while in a
        /// <see cref="ListBox"/> this method returns a <see cref="ListBoxItem"/>. Consumers of <see cref="ItemsControl"/>
        /// types can store whatever child controls they like. This method is called by GetContainerForItem() which
        /// is in turn called by <see cref="ItemContainerGenerator"/> that handles the real work of generating the items
        /// to be displayed.<para/>
        /// 
        /// Note that, for performance reasons, when the page is re-rendered, the instance that is returned from this
        /// method may be reused with a different child control, so no assumptions should be made regarding this particular
        /// <see cref="SplitterItem"/> instance and the child that is contained within it.<para/>
        /// 
        /// The <see cref="SplitterItem"/> instances returned from this method will be rendered inside of <see cref="SplitterPanel"/>,
        /// which is defined in XAML as being the "items host" of this <see cref="ItemsControl"/>.
        /// </summary>
        /// <returns>The control that items stored in this items control should be encapsulated in.</returns>
        protected override DependencyObject GetContainerForItemOverride() => new SplitterItem();
    }
}
