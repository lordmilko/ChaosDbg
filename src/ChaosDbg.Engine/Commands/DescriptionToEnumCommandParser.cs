using System;
using ChaosLib;

namespace ChaosDbg.Commands
{
    class DescriptionToEnumCommandParser<T> : ICommandParser where T : Enum
    {
        public static readonly DescriptionToEnumCommandParser<T> Instance = new();

        public object Parse(string value)
        {
            var candidates = Enum.GetValues(typeof(T));

            foreach (Enum candidate in candidates)
            {
                if (candidate.TryGetDescription(out var description))
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(description, value))
                        return candidate;
                }
            }

            return null;
        }
    }
}