using System;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Host;
using ChaosDbg.DbgEng;
using ChaosDbg.Terminal;
using ChaosLib;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.PowerShell.Host
{
    /// <summary>
    /// Represents a <see cref="PSHost"/> with ChaosDbg specific customizations
    /// </summary>
    class ChaosDbgHost : PSHostBase
    {
        private IServiceProvider serviceProvider;
        private string lastCommand;

        private HashSet<string> bannedCommands = new HashSet<string>
        {
            "ls" //List Source Lines
        };

        public ChaosDbgHost(IServiceProvider serviceProvider, CommandLineParameterParser cpp) : base(serviceProvider.GetService<ITerminal>(), cpp)
        {
            this.serviceProvider = serviceProvider;
        }

        protected override void DoRunspaceInitialization()
        {
            base.DoRunspaceInitialization();

            Runspace.SessionStateProxy.InvokeCommand.CommandNotFoundAction += OnCommandNotFound;
            Runspace.SessionStateProxy.InvokeCommand.PreCommandLookupAction += OnPreCommandLookup;
            Runspace.SessionStateProxy.InvokeCommand.PostCommandLookupAction += OnPostCommandLookup;
        }

        protected override bool TryExecuteDbgCommand(string command)
        {
            //It is of utmost importance that debugger commands are executed as quickly as possible. This is particularly
            //important when it comes to stepping. Waiting for all the gunk in System.Management.Automation to do its thing is unacceptable.
            //Therefore, we will try and intercept any known commands and handle them directly

            //Note that holding down enter when using DbgShell only "feels" faster because its prompt is two lines instead of one,
            //so emits lines twice as fast

            if (bannedCommands.Contains(command) || command.Contains("$"))
                return false;

            if (command == string.Empty)
            {
                if (!string.IsNullOrEmpty(lastCommand))
                    command = lastCommand;
                else
                    return false;
            }

            var dbgEngEngineProvider = serviceProvider.GetService<DbgEngEngineProvider>();

            var engine = dbgEngEngineProvider.ActiveEngine;

            if (engine != null)
            {
                bool success = false;

                //We've got a bit of an issue. If we do "g" we're going to go...but because PSReadLine disables the normal handling
                //of Ctrl+C, we can't install a Ctrl+C hook to say to interrupt the target. In fact, it's even worse than that:
                //PSReadLine's ReadKey Thread won't even begin listening for a key until _readKeyWaitHandle is signalled.
                //We can potentially handle Ctrl+C ourselves, but then we'd have a race between the ReadKey Thread going back to sleep
                //(it may be in the middle of calling ReadConsoleInput at the point that this code is running) and the code that runs
                //on the engine thread.

                //If we're not stepping, restore the normal Ctrl+C handler so that we can stop the command if it's long running
                var oldMode = terminal.GetInputConsoleMode();

                var newMode = oldMode | ConsoleMode.ENABLE_PROCESSED_INPUT;

                //If we're doing any kind of stepping, we may try and quickly do a step in while the current step
                //is still processing. If we restore normal Ctrl+C behavior, this will also restore F11 full screen
                //behavior. So only restore normal Ctrl+C behavior if we're not doing a step
                if (oldMode != newMode && command != "p" && command != "t")
                    terminal.SetInputConsoleMode(newMode);

                try
                {
                    using (new CtrlCHandler(_ =>
                    {
                        engine.ActiveClient.Control.SetInterrupt(DEBUG_INTERRUPT.ACTIVE);
                        return true;
                    }))
                    {
                        engine.Session.EngineThread.Invoke(() =>
                        {
                            //We must write the output of the buffered command from within the engine thread so that, if the command
                            //we executed resulted in the debugger continuing, we don't race between emitting the results of the command
                            //we just executed and emitting whatever the debugger outputs next

                            var result = engine.Session.ExecuteBufferedCommand(c =>
                {
                                if (c.Control.TryExecute(DEBUG_OUTCTL.THIS_CLIENT, command, DEBUG_EXECUTE.DEFAULT) == HRESULT.S_OK)
                                    success = true;
                            });

                            if (success)
                    {
                                foreach (var line in result)
                                    UI.WriteLine(line);
                            }
                        });

                        if (success)
                    {
                            //If the command we executed resulted in the engine status changing, the break event will have been reset and
                            //the input loop will break and the engine will automatically call WaitForEvent
                            engine.WaitForBreak();

                            if (lastCommand != "g")
                                lastCommand = command;
                            else
                                lastCommand = null;

                            return true;
                        }
                    }
                }
                finally
                {
                    if (oldMode != newMode)
                        terminal.SetInputConsoleMode(oldMode);
                }
            }

            return false;
        }

        private void OnCommandNotFound(object sender, CommandLookupEventArgs e)
        {
            //When PowerShell fails to resolve a given name, it will try again with the given value prefixed by "get-"
            if (e.CommandName.StartsWith("get-"))
                return;

            //If we have any arguments, we know there was a space between the command name and the arguments (hence how these values were able to be stored in $args in the first place)
            //But if we don't have any arguments, don't add a space after the command name; 'dx' is a valid command, 'dx ' is not and will give an error that an expression was missing
            e.CommandScriptBlock = ScriptBlock.Create($@"
if($args)
{{
    Invoke-DbgCommand ('{e.CommandName}' + ' ' + $args)
}}
else
{{
    Invoke-DbgCommand '{e.CommandName}'
}}
");
        }

        private void OnPreCommandLookup(object sender, CommandLookupEventArgs e)
        {
            //throw new System.NotImplementedException();
        }

        private void OnPostCommandLookup(object sender, CommandLookupEventArgs e)
        {
            //throw new System.NotImplementedException();
        }
    }
}
