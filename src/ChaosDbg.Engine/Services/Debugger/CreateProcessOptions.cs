﻿namespace ChaosDbg
{
    public class CreateProcessOptions
    {
        public string CommandLine { get; }

        public bool StartMinimized { get; set; }

        public CreateProcessOptions(string commandLine)
        {
            CommandLine = commandLine;
        }

        public override string ToString()
        {
            return CommandLine;
        }
    }
}
