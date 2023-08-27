using System;
using System.Windows.Media;
using ChaosDbg.Scroll;

namespace ChaosDbg.Render
{
    //Rather than rendering custom content straight to the DrawingContext,
    //we instead render it to a DrawingGroup that we then render to the original
    //DrawingContext. This lets us then get at the DrawingGroup from within
    //unit tests and inspect the elements that were rendered

    public class TwoStageRenderManager
    {
        public DrawingGroup DrawingGroup { get; } = new DrawingGroup();

        private IRenderable renderer;
        private Action<DrawingContext> baseRender;

        public bool IsBaseRendering { get; private set; }

        public TwoStageRenderManager(IRenderable renderer, Action<DrawingContext> baseRender)
        {
            this.renderer = renderer;
            this.baseRender = baseRender;
        }

        public void Render(DrawingContext originalContext, ScrollManager scrollManager)
        {
            try
            {
                IsBaseRendering = true;

                baseRender(originalContext);
            }
            finally
            {
                IsBaseRendering = false;
            }

            using (var internalCtx = DrawingGroup.Open())
                renderer.Render(internalCtx, scrollManager);

            originalContext.DrawDrawing(DrawingGroup);
        }
    }
}
