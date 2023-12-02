﻿using System;
using System.CommandLine.Parsing;
using System.Threading;
using ChaosDbg;
using ChaosDbg.Cordb;

namespace chaos
{
    class CordbClient
    {
        private ManualResetEventSlim wakeEvent = new ManualResetEventSlim(false);

        private CordbEngine engine;
        private Parser commandDispatcher;

        public CordbClient(CordbEngine engine, CommandBuilder commandBuilder)
        {
            this.engine = engine;
            commandDispatcher = commandBuilder.Build();
        }

        public void Execute(string executable, bool minimized)
        {
            Console.CancelKeyPress += Console_CancelKeyPress;

            RegisterCallbacks();

            engine.CreateProcess(executable, minimized);

            EngineLoop();
        }

        private void EngineLoop()
        {
            while (true)
            {
                wakeEvent.Wait();

                InputLoop();

                wakeEvent.Reset();
            }
        }

        private void InputLoop()
        {
            while (engine.Target.Status == EngineStatus.Break)
            {
                PrintPrompt();

                var command = Console.ReadLine();

                if (!string.IsNullOrEmpty(command))
                    ExecuteCommand(command);
            }
        }

        private void PrintPrompt()
        {
            Console.Write("chaos> ");
        }

        private void ExecuteCommand(string command)
        {
            var parseResult = commandDispatcher.Parse(command);

            if (parseResult.Errors.Count > 0)
                Console.WriteLine($"Invalid command '{command}'");
            else
            {
                commandDispatcher.Invoke(command);
            }
        }

        private void PrintModules()
        {
            foreach (var module in engine.ActiveProcess.Modules)
            {
                Console.WriteLine(module);
            }
        }

        private void RegisterCallbacks()
        {
            engine.EngineStatusChanged += (s, e) =>
            {
                if (e.NewStatus == EngineStatus.Break)
                    wakeEvent.Set();
            };

            engine.ModuleLoad += (s, e) => Console.WriteLine($"ModLoad: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;
            engine.ModuleUnload += (s, e) => Console.WriteLine($"ModUnload: {e.Module.BaseAddress:X} {e.Module.EndAddress:X}   {e.Module}"); ;

            engine.ThreadCreate += (s, e) => Console.WriteLine($"ThreadCreate {e.Thread}");
            engine.ThreadExit += (s, e) => Console.WriteLine($"ThreadExit {e.Thread}");
        }

        private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            //Don't terminate our process!
            e.Cancel = true;

            engine.Break();
        }
    }
}
