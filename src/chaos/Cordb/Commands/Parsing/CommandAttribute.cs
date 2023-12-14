using System;

namespace chaos.Cordb.Commands
{
    class CommandAttribute : Attribute
    { 
        public string Name { get; }

        public CommandAttribute(string name)
        {
            Name = name;
        }
    }
}
