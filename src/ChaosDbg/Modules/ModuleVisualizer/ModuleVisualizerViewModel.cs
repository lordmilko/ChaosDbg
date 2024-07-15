using ChaosDbg.Reactive;

namespace ChaosDbg.ViewModel
{
    public partial class ModuleVisualizerViewModel : ViewModelBase
    {
        [Reactive]
        public virtual ModuleVisualizerContent RenderContent { get; set; }

        public ModuleVisualizerViewModel(DebugEngineProvider engineProvider)
        {
            RenderContent = new ModuleVisualizerContent();

            engineProvider.ModuleLoad += (s, e) => RenderContent.AddModule(e.Module);
            engineProvider.ModuleUnload += (s, e) => RenderContent.RemoveModule(e.Module);
        }
    }
}
