using System;

namespace ChaosDbg
{
    public class UnknownEnumValueException : NotImplementedException
    {
        public UnknownEnumValueException(object value) : base($"Don't know how to handle {value.GetType().Name} value '{value}'.")
        {
        }
    }
}
