using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using ChaosDbg.PowerShell.Cmdlets.Control;
using ChaosDbg.Terminal;

namespace ChaosDbg.PowerShell.Host
{
    /// <summary>
    /// Represents a <see cref="PSHost"/> with ChaosDbg specific customizations
    /// </summary>
    class ChaosDbgHost : PSHostBase
    {
        private IServiceProvider serviceProvider;

        private List<string> powerShellCommands = new List<string>
        {
            "wt", //Windows Terminal
            "gu", //Get-Unique
            "r" //Invoke-History
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
            if (command == null)
                return false;

            /* It is of utmost importance that debugger commands are executed as quickly as possible. This is particularly
             * important when it comes to stepping. Waiting for all the gunk in System.Management.Automation to do its thing is unacceptable.
             * Therefore, we will try and intercept any known commands and handle them directly
             *
             * Note that holding down enter when using DbgShell only "feels" faster because its prompt is two lines instead of one,
             * so emits lines twice as fast */

            switch (command)
            {
                case "t":
                case "p":
                    break;

                default:
                    //If it's a dx command, they might be typing @$cursession or something. PowerShell will interpret $cursession as being a variable, so we need to intercept this

                    //There's several possibilities:
                    //1. It's a sequence of PowerShell commands
                    //2. It's a sequence of DbgEng commands
                    //3. There's a combination of PowerShell and DbgEng commands
                    //4. There's a DbgEng loop containing multiple commands
                    //5. There's a PowerShell loop containing multiple commands
                    //6. There's a PowerShell loop containing both PowerShell and DbgEng commands
                    if (command.Contains(";")) //if they typed a ; they might be trying to chain multiple dbgeng or powershell commands. We need to split the input and process each command separately
                        return false;

                    if (command.StartsWith("dx "))
                        break;

                    if (powerShellCommands.Any(c => c == command || command.StartsWith($"{c} ")))
                        break;

                    return false;
            }

            var invokeDbgCommand = new InvokeDbgCommand
            {
                Command = new[]{command}
            };

            invokeDbgCommand.Execute();

            return true;
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
