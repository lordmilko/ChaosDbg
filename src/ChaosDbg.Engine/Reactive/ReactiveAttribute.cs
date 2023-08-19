using System;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Specifies that a given property is reactive, and that a dynamically
    /// generated reactive proxy this override the property to call OnPropertyChanged()
    /// when modified.<para/>
    /// The property must be declared as <see langword="virtual"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal class ReactiveAttribute : Attribute
    {
    }
}