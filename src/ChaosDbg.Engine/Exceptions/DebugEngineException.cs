using System;

namespace ChaosDbg
{
    /// <summary>
    /// The exception that is thrown when a critical error occurs inside of a debugger.
    /// </summary>
    class DebugEngineException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DebugEngineException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public DebugEngineException(string message) : base(message)
        {
        }
    }
}
