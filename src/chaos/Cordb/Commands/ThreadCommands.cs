using System;
using System.Collections.Generic;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos.Cordb.Commands
{
    [Command("~")]
    class ThreadCommands : CommandBase, ICustomCommandParser
    {
        private StackTraceCommands StackTraceCommand { get; }

        public ThreadCommands(
            IConsole console,
            CordbEngineProvider engineProvider,
            StackTraceCommands stackTraceCommand) : base(console, engineProvider)
        {
            StackTraceCommand = stackTraceCommand;
        }

        // ~[<id>]
        public void ListThreads(ThreadArg threadArg)
        {
            threadArg.Execute(
                none: () =>
                {
                    foreach (var thread in engine.Process.Threads)
                    {
                        if (engine.Process.Threads.ActiveThread == thread)
                            Console.Write("* ");

                        Console.WriteLine(thread);
                    }
                },

                current: () =>
                {
                    Console.WriteLine(engine.Process.Threads.ActiveThread);
                },

                number: id =>
                {
                    throw new NotImplementedException();
                },

                all: () =>
                {
                    throw new NotImplementedException();
                }
            );
        }

        // ~[<id>]g
        public void Go(ThreadArg threadArg)
        {
            threadArg.Execute(
                none: () =>
                {
                    throw new NotImplementedException();
                },

                current: () =>
                {
                    throw new NotImplementedException();
                },

                number: id =>
                {
                    throw new NotImplementedException();
                },

                all: () =>
                {
                    throw new NotImplementedException();
                }
            );
        }

        // ~[<id>]k
        public void StackTrace(ThreadArg threadArg)
        {
            threadArg.Execute(
                none:    () => StackTraceCommand.StackTrace(),
                current: () => StackTraceCommand.StackTrace(),

                number: id =>
                {
                    throw new NotImplementedException();
                },

                all: () =>
                {
                    var threads = engine.Process.Threads;

                    foreach (var thread in threads)
                    {
                        Console.WriteLine(thread.Id);

                        foreach (var frame in thread.StackTrace)
                            Console.WriteLine("    " + frame);
                    }
                }
            );
        }

        // ~[<id>]s
        public void SetActiveThread(ThreadArg threadArg)
        {
            threadArg.Execute(
                none: () =>
                {
                    throw new NotImplementedException();
                },

                current: () =>
                {
                    throw new NotImplementedException();
                },

                number: id =>
                {
                    if (engine.Process.Threads.TryGetThreadByUserId(id, out var thread))
                    {
                        engine.Process.Threads.ActiveThread = thread;
                    }
                },

                all: () => throw new InvalidCommandException("Illegal thread error")
            );
        }

        #region ICustomCommandParser

        Action ICustomCommandParser.Parse(ArgParser args)
        {
            if (args.Empty)
                return () => ListThreads(default);

            var ch = args.Next();

            ThreadArg thread = default;

            if (ch == '.')
                thread = new ThreadArg(ThreadArgKind.Current);
            else if (ch == '#')
                thread = new ThreadArg(ThreadArgKind.Event);
            else if (ch == '*')
                thread = new ThreadArg(ThreadArgKind.All);

            var nums = new List<char>();

            while (ch >= '0' && ch <= '9')
            {
                nums.Add(ch);
                ch = args.Next();
            }

            if (nums.Count > 0)
            {
                var threadId = Convert.ToInt32(new string(nums.ToArray()));
                thread = new ThreadArg(threadId);
            }
            else
                ch = args.Next();

            switch (ch)
            {
                case '\0':
                    return () => ListThreads(thread);

                case 'k':
                    return () => StackTrace(thread);

                case 's':
                    return () => SetActiveThread(thread);

                default:
                    args.ErrorMessage = $"Unknown option '{ch}'";
                    return null;
            }
        }

        #endregion
    }
}
