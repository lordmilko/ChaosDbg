using System;
using System.Text;

namespace ChaosDbg.Metadata
{
    public interface ISigCustomAttribute
    {
    }

    public class SigCustomAttribute : ISigCustomAttribute
    {
        public static readonly SigCustomAttribute UnsupportedAttribute = new SigCustomAttribute("UnsupportedAttribute]", Array.Empty<ISigCustomAttribFixedArg>(), Array.Empty<ISigCustomAttribNamedArg>());

        public string Name { get; }

        public ISigCustomAttribFixedArg[] FixedArgs { get; }

        public ISigCustomAttribNamedArg[] NamedArgs { get; }

        public SigCustomAttribute(string name, ISigCustomAttribFixedArg[] fixedArgs, ISigCustomAttribNamedArg[] namedArgs)
        {
            Name = name;
            FixedArgs = fixedArgs;
            NamedArgs = namedArgs;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(Name);

            return builder.ToString();
        }
    }
}
