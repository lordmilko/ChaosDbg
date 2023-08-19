using ChaosDbg.Reactive;
using ChaosDbg.ViewModel;

namespace ChaosDbg.Tests
{
    class SuccessViewModel : ViewModelBase
    {
        [Reactive]
        public virtual string Value { get; set; }
    }

    class SetValueInCtorViewModel : ViewModelBase
    {
        [Reactive]
        public virtual string Value { get; set; }

        public SetValueInCtorViewModel()
        {
            Value = "val";
        }
    }

    class NoReactivePropertiesViewModel : ViewModelBase
    {
        public virtual string Value { get; set; }
    }

    class NonVirtualPropertyViewModel : ViewModelBase
    {
        [Reactive]
        public string Value { get; set; }
    }

    class MissingSetterViewModel : ViewModelBase
    {
        [Reactive]
        public virtual string Value { get; }
    }

    class StartsWithLowercaseViewModel : ViewModelBase
    {
        [Reactive]
        public virtual string value { get; }
    }
}
