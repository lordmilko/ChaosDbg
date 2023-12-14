using System;

namespace chaos.Cordb.Commands
{
    [AttributeUsage(AttributeTargets.Parameter)]
    class OptionAttribute : Attribute
    {
        public string Name { get; }

        public OptionAttribute(string name)
        {
            Name = name;
        }
    }
}
