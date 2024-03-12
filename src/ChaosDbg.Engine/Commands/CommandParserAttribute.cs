using System;

namespace ChaosDbg.Commands
{
    public class CommandParserAttribute : Attribute
    {
        public Type Type { get; }

        public CommandParserAttribute(Type type)
        {
            Type = type;
        }
    }
}
