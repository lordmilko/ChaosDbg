using System;

namespace ChaosDbg.Commands
{
    class TrimUnderscoreEnumCommandParser<T> : ICommandParser
    {
        public static readonly TrimUnderscoreEnumCommandParser<T> Instance = new();
        public object Parse(string value)
        {
            var candidates = Enum.GetValues(typeof(T));

            foreach (var candidate in candidates)
            {
                if (candidate.ToString().TrimStart('_') == value)
                    return candidate;
            }

            return null;
        }
    }
}