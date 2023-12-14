using System.CommandLine.Parsing;

namespace chaos
{
    class RelayParser
    {
        private Parser parser;
        private string[] customCommands;

        public RelayParser(Parser parser, string[] customCommands)
        {
            this.parser = parser;
            this.customCommands = customCommands;
        }

        public ParseResult Parse(string commandLine)
        {
            if (commandLine != null && commandLine.Length > 0)
            {
                foreach (var command in customCommands)
                {
                    if (commandLine.StartsWith(command))
                    {
                        if (commandLine.Length == 1)
                            break;
                        else
                        {
                            commandLine = commandLine[0] + " " + commandLine.Substring(1);
                            break;
                        }
                    }
                }
            }

            return parser.Parse(commandLine);
        }

        public int Invoke(string commandLine) => Parse(commandLine).Invoke();
    }
}
