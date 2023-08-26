namespace ChaosDbg.Render
{
    public interface IRenderManagerProvider
    {
        TwoStageRenderManager RenderManager { get; }
    }
}
