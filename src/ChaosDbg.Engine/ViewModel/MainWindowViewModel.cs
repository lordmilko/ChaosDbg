using ChaosDbg.Reactive;
using ChaosDbg.Text;

namespace ChaosDbg.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        [Reactive]
        public virtual ITextBuffer Buffer { get; set; }

        public MainWindowViewModel(ITextBufferProvider textBufferProvider)
        {
            Buffer = textBufferProvider.GetBuffer();
        }
    }
}
