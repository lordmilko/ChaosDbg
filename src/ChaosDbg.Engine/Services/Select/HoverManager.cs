using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ChaosDbg.Scroll;
using ChaosDbg.Text;

namespace ChaosDbg.Select
{
    public class HoverManager
    {
        private FrameworkElement owner;
        private DispatcherTimer hoverDispatcher;

        private ToolTip tooltip;
        private ITextBehavior currentBehavior;

        public HoverManager(FrameworkElement owner)
        {
            this.owner = owner;

            hoverDispatcher = new DispatcherTimer(
                interval: TimeSpan.FromMilliseconds(250),
                priority: DispatcherPriority.Render,
                callback: HoverCallback,
                owner.Dispatcher
            );
        }

        public void Start(ITextBehavior behavior)
        {
            currentBehavior = behavior;
            hoverDispatcher.Start();
        }

        public void Clear()
        {
            hoverDispatcher.Stop();

            currentBehavior = null;

            if (tooltip == null)
                return;

            tooltip.IsOpen = false;
            tooltip = null;
        }

        private void HoverCallback(object sender, EventArgs e)
        {
            hoverDispatcher.Stop();

            if (currentBehavior == null)
                return;

            if (currentBehavior.HoverText != null)
            {
                var scrollManager = ((IScrollable) owner).ScrollManager;
                var zoom = scrollManager.Zoom;

                tooltip = new ToolTip
                {
                    Content = new TextBlock
                    {
                        Text = currentBehavior.HoverText
                    },
                    LayoutTransform = new ScaleTransform(zoom, zoom),
                    IsOpen = true
                };
            }
        }
    }
}
