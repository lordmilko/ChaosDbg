using System.Collections.ObjectModel;

namespace ChaosDbg.ViewModel
{
    public class ModulesPaneViewModel : ViewModelBase
    {
        public ObservableCollection<IDbgModule> Modules { get; } = new ObservableCollection<IDbgModule>();

        public ModulesPaneViewModel(DebugEngineProvider engineProvider)
        {
            engineProvider.ModuleLoad += (s, e) => Modules.Add(e.Module);
            engineProvider.ModuleUnload += (s, e) => Modules.Remove(e.Module);
        }
    }
}
