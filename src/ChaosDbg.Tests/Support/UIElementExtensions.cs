using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace ChaosDbg.Tests
{
    public static class UIElementExtensions
    {
        private static MethodInfo UIElement_OnRender;

        static UIElementExtensions()
        {
            UIElement_OnRender = typeof(UIElement).GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static DrawingGroup Render(this UIElement element)
        {
            var drawingGroup = new DrawingGroup();

            using (var ctx = drawingGroup.Open())
                UIElement_OnRender.Invoke(element, new object[] { ctx });

            return drawingGroup;
        }
    }
}
