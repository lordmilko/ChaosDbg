using System;

namespace chaos.Cordb.Commands
{
    class InvalidCommandException : Exception
    {
        public InvalidCommandException(string message) : base(message)
        {
        }
    }
}
