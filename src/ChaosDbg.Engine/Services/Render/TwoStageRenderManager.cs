using System;
using System.Windows.Media;

namespace ChaosDbg.Render
{
    public class TwoStageRenderManager
    {
        public DrawingGroup DrawingGroup { get; } = new DrawingGroup();

        private IRenderer renderer;
        private Action<DrawingContext> baseRender;

        public TwoStageRenderManager(IRenderer renderer, Action<DrawingContext> baseRender)
        {
            this.renderer = renderer;
            this.baseRender = baseRender;
        }

        public void Render(DrawingContext originalContext)
        {
            baseRender(originalContext);

            using (var internalCtx = DrawingGroup.Open())
                renderer.Render(internalCtx);

            originalContext.DrawDrawing(DrawingGroup);
        }
    }
}
