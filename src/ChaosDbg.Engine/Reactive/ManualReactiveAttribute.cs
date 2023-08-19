using System;

namespace ChaosDbg.Reactive
{
    /// <summary>
    /// Specifies that a given property is reactive, but that its implementation should
    /// not be dynamically generated. This attribute has no functional effect.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ManualReactiveAttribute : Attribute
    {
    }
}