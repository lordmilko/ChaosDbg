using System;
using System.Windows;
using System.Windows.Input;
using ChaosDbg.Scroll;
using ChaosDbg.Text;
using static ChaosDbg.EventExtensions;

namespace ChaosDbg.Select
{
    public class MouseManager
    {
        private FrameworkElement owner;

        private ScrollManager ScrollManager => ((IScrollable) owner).ScrollManager;

        private IUiTextBuffer Content => (IUiTextBuffer) ScrollManager.ScrollArea;

        private HoverManager HoverManager { get; }

        public ITextRun MousedOverTextRun { get; private set; }

        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        private TextPosition lastLeftDownPos;
        private ITextRun lastLeftDownRun;

        private TextRange selection;

        public MouseManager(FrameworkElement owner)
        {
            this.owner = owner;

            HoverManager = new HoverManager(owner);
        }

        public void OnLeftDown(MouseButtonEventArgs mouseButtonEventArgs)
        {
            mouseButtonEventArgs.Handled = true;

            var pos = mouseButtonEventArgs.GetPosition(owner);

            var cursorPos = GetAbsoluteTextPositionFromPoint(pos);
            var textRun = GetAbsoluteTextRunAtPoint(pos);

            selection = new TextRange(cursorPos, cursorPos);

            lastLeftDownPos = cursorPos;
            lastLeftDownRun = textRun;

            HandleEvent(SelectionChanged, this, new SelectionChangedEventArgs(selection, Content.Buffer.GetTextForRange(selection)));
        }

        public void OnLeftUp(MouseButtonEventArgs mouseButtonEventArgs)
        {
            lastLeftDownRun = null;
        }

        public void OnRightDown(MouseButtonEventArgs mouseButtonEventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnMove(MouseEventArgs mouseEventArgs)
        {
            if (ScrollManager == null)
                return;

            var pos = mouseEventArgs.GetPosition(owner);

            if (lastLeftDownRun != null)
            {
                var cursorPos = GetAbsoluteTextPositionFromPoint(pos);
                selection.End = cursorPos;

                var text = Content.Buffer.GetTextForRange(selection);

                HandleEvent(SelectionChanged, this, new SelectionChangedEventArgs(selection, text));
            }

            var textRun = GetAbsoluteTextRunAtPoint(pos);

            if (textRun != MousedOverTextRun && MousedOverTextRun?.Behavior != null)
                MousedOverTextRun.Behavior.OnMouseLeave();

            MousedOverTextRun = textRun;

            ProcessMouseOver();
        }

        public void OnLeave(MouseEventArgs mouseEventArgs)
        {
            if (MousedOverTextRun?.Behavior != null)
                MousedOverTextRun.Behavior.OnMouseLeave();

            MousedOverTextRun = null;

            HoverManager.Clear();
        }

        public void OnLostCapture(MouseEventArgs mouseEventArgs)
        {
            throw new NotImplementedException();
        }

        #region Move

        private void ProcessMouseOver()
        {
            HoverManager.Clear();

            var behavior = MousedOverTextRun?.Behavior;

            owner.Cursor = behavior?.MouseCursor ?? Cursors.IBeam;

            if (behavior != null)
            {
                HoverManager.Start(behavior);

                behavior.OnMouseEnter();
            }
        }

        #endregion
        #region Helpers

        //Point -> Absolute Point -> TextPosition -> ITextRun
        private ITextRun GetAbsoluteTextRunAtPoint(Point mousePosition) =>
            Content.Buffer.GetRunAtTextPosition(Content.GetTextPositionFromPoint(GetAbsolutePositionFromMousePosition(mousePosition), true));

        //Point -> Absolute Point -> TextPosition
        private TextPosition GetAbsoluteTextPositionFromPoint(Point mousePosition) =>
            Content.GetTextPositionFromPoint(GetAbsolutePositionFromMousePosition(mousePosition), false);

        //Point -> Absolute Point
        private Point GetAbsolutePositionFromMousePosition(Point mousePosition)
        {
            var absolutePos = new Point(
                mousePosition.X / ScrollManager.Zoom + ScrollManager.ScrollPosition.X,
                mousePosition.Y / ScrollManager.Zoom + ScrollManager.ScrollPosition.Y
            );

            return absolutePos;
        }

        #endregion
    }
}
