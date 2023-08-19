using System;
using System.Linq;
using System.Reflection;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Stores information about a reactive proxy - a dynamically generated type with
    /// reactive properties that automatically dispatch to OnPropertyChanged when modified
    /// </summary>
    public class ReactiveProxyInfo
    {
        public Type Type { get; }

        public ConstructorInfo ConstructorInfo { get; }

        public ReactiveProxyInfo(Type type)
        {
            Type = type;
            ConstructorInfo = type.GetConstructors().Single();
        }

        public T CreateInstance<T>(params object[] args)
        {
            try
            {
                return (T) Activator.CreateInstance(Type, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }
    }
}