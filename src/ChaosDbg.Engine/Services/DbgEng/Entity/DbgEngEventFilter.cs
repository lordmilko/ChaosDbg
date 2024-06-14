using System;
using System.Diagnostics;
using System.Text;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents an event filter used to respond to debugger events (e.g. module/thread load) that in occur in a debug target.
    /// </summary>
    public class DbgEngEngineEventFilter : DbgEngEventFilter, IDbgEngineEventFilter
    {
        public DEBUG_FILTER_EVENT Code { get; }

        public string Argument { get; }

        public DbgEngEngineEventFilter(int index, string name, string alias, in DEBUG_SPECIFIC_FILTER_PARAMETERS filter, string command, string argument) : base(index, name, alias, filter.ExecutionOption, filter.ContinueOption, command)
        {
            Code = (DEBUG_FILTER_EVENT) index;
            Argument = argument;
        }
    }

    public class DbgEngExceptionEventFilter : DbgEngEventFilter, IDbgExceptionEventFilter
    {
        public NTSTATUS Code { get; }

        public string SecondCommand { get; }

        public DbgEngExceptionEventFilter(int index, string name, string alias, in DEBUG_EXCEPTION_FILTER_PARAMETERS filter, string command, string secondCommand) : base(index, name, alias, filter.ExecutionOption, filter.ContinueOption, command)
        {
            Code = filter.ExceptionCode;
            SecondCommand = secondCommand;
        }
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class DbgEngEventFilter : IDbgEventFilter
    {
        public int Index { get; }
        public string Name { get; }
        public string Alias { get; }
        public string Command { get; }

        public DEBUG_FILTER_EXEC_OPTION ExecutionOption { get; }
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay
        {
            get
            {
                var builder = new StringBuilder();

                builder.Append("[").Append(Alias).Append("]").Append(" ").Append(Name);

                if (this is DbgEngExceptionEventFilter e)
                {
                    builder.Append(" -> ");

                    if (Enum.IsDefined(typeof(NTSTATUS), e.Code))
                        builder.Append(e.Code);
                    else
                        builder.Append("0x").Append(((uint) e.Code).ToString("x"));
                }

                return builder.ToString();
            }
        }

        protected DbgEngEventFilter(int index, string name, string alias, DEBUG_FILTER_EXEC_OPTION executionOption, DEBUG_FILTER_CONTINUE_OPTION continueOption, string command)
        {
            Index = index;
            Name = name;
            Alias = alias;
            ExecutionOption = executionOption;
            ContinueOption = continueOption;
            Command = command;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
