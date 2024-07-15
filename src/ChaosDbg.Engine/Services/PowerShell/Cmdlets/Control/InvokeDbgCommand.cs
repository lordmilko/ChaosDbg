using System;
using System.Management.Automation;
using ChaosDbg.Terminal;
using ChaosLib;

namespace ChaosDbg.PowerShell.Cmdlets.Control
{
    [Cmdlet(VerbsLifecycle.Invoke, "DbgCommand")]
    public class InvokeDbgCommand : DbgEngCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromRemainingArguments = true)]
        public string[] Command { get; set; }

        protected override void ProcessRecord()
        {
            var command = string.Join(string.Empty, Command);

            //If we're fast invoking F10 or F11, don't mess with the input mode;
            //we don't need to intercept Ctrl+C anyway
            using var consoleModeHolder = new ConsoleProcessedInputHolder(
                GetService<ITerminal>(),
                command != WellKnownCommand.StepOver && command != WellKnownCommand.StepOver
            );

            using var ctrlCHolder = new CtrlCHandler(HandleCtrlC);

            ActiveEngine.Execute(command);

            ActiveEngine.WaitForBreak();
        }

        private bool HandleCtrlC(ConsoleControlType dwctrltype)
        {
            throw new NotImplementedException();
        }
    }
}
