using System;
using System.Text;

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

        public string Eat(int count = -1)
        {
            if (count == -1)
            {
                //Consume all remaining chars
                if (current >= chars.Length)
                    throw new InvalidProgramException("There are no more characters left to eat");

                var result = new StringBuilder();

                while (true)
                {
                    var ch = Next(false);

                    if (ch == '\0')
                        break;
                    else
                        result.Append(ch);
                }

                return result.ToString();
            }
            else
                throw new NotImplementedException("Eating a specific number of characters is not implemented");
        }

        public ArgParser(string args)
        {
            if (args == null)
                chars = Array.Empty<char>();
            else
                chars = args.ToCharArray();
        }

        public char Next(bool skipSpaces = true)
        {
            while (true)
            {
                if (current >= chars.Length)
                    return '\0';

                var ch = chars[current];
                current++;

                if (ch == ' ' && skipSpaces)
                    continue;

                return ch;
            }
        }
    }
}
