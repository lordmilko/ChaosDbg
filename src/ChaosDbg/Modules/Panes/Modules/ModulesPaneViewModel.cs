using System.Collections.ObjectModel;
using ChaosDbg.DbgEng;

namespace ChaosDbg.ViewModel
{
    public class ModulesPaneViewModel : ViewModelBase
    {
        public ObservableCollection<DbgEngModule> Modules { get; } = new ObservableCollection<DbgEngModule>();

        public ModulesPaneViewModel(DbgEngEngine engine)
        {
            engine.ModuleLoad += (s, e) => Modules.Add(e.Module);
            engine.ModuleUnload += (s, e) => Modules.Remove(e.Module);
        }
    }
}
