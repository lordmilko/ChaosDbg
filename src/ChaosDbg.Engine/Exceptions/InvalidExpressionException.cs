using System;

namespace ChaosDbg
{
    public class InvalidExpressionException : Exception
    {
        public InvalidExpressionException(string message) : base(message)
        {
        }
    }
}
