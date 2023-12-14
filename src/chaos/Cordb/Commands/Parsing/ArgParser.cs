using System;

namespace chaos
{
    /// <summary>
    /// Provides facilities for manually parsing a string of argument characters without spaces.
    /// </summary>
    class ArgParser
    {
        char[] chars;
        private int current;

        public bool Empty => chars.Length == 0;

        public bool End => current >= chars.Length;

        public string ErrorMessage { get; set; }

        public string Remaining
        {
            get
            {
                if (End)
                    return string.Empty;

                return new string(chars, current, chars.Length - current);
            }
        }

        public ArgParser(string args)
        {
            if (args == null)
                chars = Array.Empty<char>();
            else
                chars = args.ToCharArray();
        }

        public char Next()
        {
            if (current > chars.Length)
                return '\0';

            var ch = chars[current];
            current++;

            if (ch == ' ')
                return Next();

            return ch;
        }
    }
}
