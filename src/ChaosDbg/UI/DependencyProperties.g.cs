using System.Windows;
using System.Windows.Controls;

namespace ChaosDbg
{
    public partial class DockContainer
    {
        #region DockedWidth

        public static readonly DependencyProperty DockedWidthProperty = DependencyProperty.Register(
            name: nameof(DockedWidth),
            propertyType: typeof(SplitPaneLength),
            ownerType: typeof(DockContainer),
            typeMetadata: new FrameworkPropertyMetadata(new SplitPaneLength(100.0))
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="DockedWidthProperty" /> dependency property.
        /// </summary>
        public SplitPaneLength DockedWidth
        {
            get => (SplitPaneLength) GetValue(DockedWidthProperty);
            set => SetValue(DockedWidthProperty, value);
        }

        #endregion
        #region DockedHeight

        public static readonly DependencyProperty DockedHeightProperty = DependencyProperty.Register(
            name: nameof(DockedHeight),
            propertyType: typeof(SplitPaneLength),
            ownerType: typeof(DockContainer),
            typeMetadata: new FrameworkPropertyMetadata(new SplitPaneLength(100.0))
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="DockedHeightProperty" /> dependency property.
        /// </summary>
        public SplitPaneLength DockedHeight
        {
            get => (SplitPaneLength) GetValue(DockedHeightProperty);
            set => SetValue(DockedHeightProperty, value);
        }

        #endregion
        #region MinimumWidth

        public static readonly DependencyProperty MinimumWidthProperty = DependencyProperty.Register(
            name: nameof(MinimumWidth),
            propertyType: typeof(double),
            ownerType: typeof(DockContainer),
            typeMetadata: new FrameworkPropertyMetadata(30.0)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="MinimumWidthProperty" /> dependency property.
        /// </summary>
        public double MinimumWidth
        {
            get => (double) GetValue(MinimumWidthProperty);
            set => SetValue(MinimumWidthProperty, value);
        }

        #endregion
        #region MinimumHeight

        public static readonly DependencyProperty MinimumHeightProperty = DependencyProperty.Register(
            name: nameof(MinimumHeight),
            propertyType: typeof(double),
            ownerType: typeof(DockContainer),
            typeMetadata: new FrameworkPropertyMetadata(30.0)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="MinimumHeightProperty" /> dependency property.
        /// </summary>
        public double MinimumHeight
        {
            get => (double) GetValue(MinimumHeightProperty);
            set => SetValue(MinimumHeightProperty, value);
        }

        #endregion
    }

    public partial class SplitterItemsDockContainer
    {
        #region Orientation

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            name: nameof(Orientation),
            propertyType: typeof(Orientation),
            ownerType: typeof(SplitterItemsDockContainer)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="OrientationProperty" /> dependency property.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation) GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        #endregion
    }

    public partial class TabControlDockContainer
    {
        #region TabStripPlacement

        public static readonly DependencyProperty TabStripPlacementProperty = DependencyProperty.Register(
            name: nameof(TabStripPlacement),
            propertyType: typeof(Dock),
            ownerType: typeof(TabControlDockContainer),
            typeMetadata: new FrameworkPropertyMetadata(Dock.Bottom)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="TabStripPlacementProperty" /> dependency property.
        /// </summary>
        public Dock TabStripPlacement
        {
            get => (Dock) GetValue(TabStripPlacementProperty);
            set => SetValue(TabStripPlacementProperty, value);
        }

        #endregion
    }

    public partial class SplitterGrip
    {
        #region Orientation

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            name: nameof(Orientation),
            propertyType: typeof(Orientation),
            ownerType: typeof(SplitterGrip),
            typeMetadata: new FrameworkPropertyMetadata(Orientation.Vertical)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="OrientationProperty" /> dependency property.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation) GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        #endregion
        #region ResizeBehavior

        public static readonly DependencyProperty ResizeBehaviorProperty = DependencyProperty.Register(
            name: nameof(ResizeBehavior),
            propertyType: typeof(GridResizeBehavior),
            ownerType: typeof(SplitterGrip),
            typeMetadata: new FrameworkPropertyMetadata(GridResizeBehavior.CurrentAndNext)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="ResizeBehaviorProperty" /> dependency property.
        /// </summary>
        public GridResizeBehavior ResizeBehavior
        {
            get => (GridResizeBehavior) GetValue(ResizeBehaviorProperty);
            set => SetValue(ResizeBehaviorProperty, value);
        }

        #endregion
    }

    public partial class SplitterItemsControl
    {
        #region Orientation

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            name: nameof(Orientation),
            propertyType: typeof(Orientation),
            ownerType: typeof(SplitterItemsControl),
            typeMetadata: new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="OrientationProperty" /> dependency property.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation) GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        #endregion
        #region SplitterGripSize

        public static readonly DependencyProperty SplitterGripSizeProperty = DependencyProperty.RegisterAttached(
            name: "SplitterGripSize",
            propertyType: typeof(double),
            ownerType: typeof(SplitterItemsControl),
            defaultMetadata: new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.Inherits)
        );

        /// <summary>
        /// Gets the value of the attached <see cref="SplitterGripSizeProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static double GetSplitterGripSize(UIElement element) => (double) element.GetValue(SplitterGripSizeProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="SplitterGripSizeProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        public static void SetSplitterGripSize(UIElement element, double value) => element.SetValue(SplitterGripSizeProperty, value);

        #endregion
    }

    public partial class SplitterPanel
    {
        #region Orientation

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            name: nameof(Orientation),
            propertyType: typeof(Orientation),
            ownerType: typeof(SplitterPanel),
            typeMetadata: new FrameworkPropertyMetadata(Orientation.Vertical, FrameworkPropertyMetadataOptions.AffectsMeasure)
        );

        /// <summary>
        /// Gets or sets the value of the <see cref="OrientationProperty" /> dependency property.
        /// </summary>
        public Orientation Orientation
        {
            get => (Orientation) GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        #endregion
        #region IsFirst

        private static readonly DependencyPropertyKey IsFirstPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            name: "IsFirst",
            propertyType: typeof(bool),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(false)
        );

        public static readonly DependencyProperty IsFirstProperty = IsFirstPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the value of the attached <see cref="IsFirstProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static bool GetIsFirst(UIElement element) => (bool) element.GetValue(IsFirstProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="IsFirstProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        private static void SetIsFirst(UIElement element, bool value) => element.SetValue(IsFirstPropertyKey, value);

        #endregion
        #region IsLast

        private static readonly DependencyPropertyKey IsLastPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            name: "IsLast",
            propertyType: typeof(bool),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(false)
        );

        public static readonly DependencyProperty IsLastProperty = IsLastPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the value of the attached <see cref="IsLastProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static bool GetIsLast(UIElement element) => (bool) element.GetValue(IsLastProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="IsLastProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        private static void SetIsLast(UIElement element, bool value) => element.SetValue(IsLastPropertyKey, value);

        #endregion
        #region PaneLength

        public static readonly DependencyProperty PaneLengthProperty = DependencyProperty.RegisterAttached(
            name: "PaneLength",
            propertyType: typeof(SplitPaneLength),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(new SplitPaneLength(100.0), FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange)
        );

        /// <summary>
        /// Gets the value of the attached <see cref="PaneLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static SplitPaneLength GetPaneLength(UIElement element) => (SplitPaneLength) element.GetValue(PaneLengthProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="PaneLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        public static void SetPaneLength(UIElement element, SplitPaneLength value) => element.SetValue(PaneLengthProperty, value);

        #endregion
        #region ActualPaneLength

        private static readonly DependencyPropertyKey ActualPaneLengthPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
            name: "ActualPaneLength",
            propertyType: typeof(double),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(0.0)
        );

        public static readonly DependencyProperty ActualPaneLengthProperty = ActualPaneLengthPropertyKey.DependencyProperty;

        /// <summary>
        /// Gets the value of the attached <see cref="ActualPaneLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static double GetActualPaneLength(UIElement element) => (double) element.GetValue(ActualPaneLengthProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="ActualPaneLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        private static void SetActualPaneLength(UIElement element, double value) => element.SetValue(ActualPaneLengthPropertyKey, value);

        #endregion
        #region MinimumLength

        public static readonly DependencyProperty MinimumLengthProperty = DependencyProperty.RegisterAttached(
            name: "MinimumLength",
            propertyType: typeof(double),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(0.0)
        );

        /// <summary>
        /// Gets the value of the attached <see cref="MinimumLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static double GetMinimumLength(UIElement element) => (double) element.GetValue(MinimumLengthProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="MinimumLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        public static void SetMinimumLength(UIElement element, double value) => element.SetValue(MinimumLengthProperty, value);

        #endregion
        #region MaximumLength

        public static readonly DependencyProperty MaximumLengthProperty = DependencyProperty.RegisterAttached(
            name: "MaximumLength",
            propertyType: typeof(double),
            ownerType: typeof(SplitterPanel),
            defaultMetadata: new FrameworkPropertyMetadata(double.MaxValue)
        );

        /// <summary>
        /// Gets the value of the attached <see cref="MaximumLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to get the attached property from.</param>
        public static double GetMaximumLength(UIElement element) => (double) element.GetValue(MaximumLengthProperty);

        /// <summary>
        /// Sets the value of the attached <see cref="MaximumLengthProperty" /> dependency property for a particular element.
        /// </summary>
        /// <param name="element">The element to set the attached property on.</param>
        /// <param name="value">The value to set the attached property to.</param>
        public static void SetMaximumLength(UIElement element, double value) => element.SetValue(MaximumLengthProperty, value);

        #endregion
    }
}
