using ChaosDbg.DbgEng;
using ChaosDbg.Reactive;

namespace ChaosDbg.ViewModel
{
    public partial class ModuleVisualizerViewModel : ViewModelBase
    {
        [Reactive]
        public virtual ModuleVisualizerContent RenderContent { get; set; }

        public ModuleVisualizerViewModel(DbgEngEngine engine)
        {
            RenderContent = new ModuleVisualizerContent();

            engine.ModuleLoad += (s, e) => RenderContent.AddModule(e.Module);
            engine.ModuleUnload += (s, e) => RenderContent.RemoveModule(e.Module);
        }
    }
}
