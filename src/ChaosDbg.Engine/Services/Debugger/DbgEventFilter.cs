using System;
using System.Diagnostics;
using System.Text;
using ClrDebug;
using ClrDebug.DbgEng;

namespace ChaosDbg
{
    /// <summary>
    /// Represents an event filter used to respond to debugger events (e.g. module/thread load) that in occur in a debug target.<para/>
    /// This type is shared between multiple debugger implementations.
    /// </summary>
    public class DbgEngineEventFilter : DbgEventFilter
    {
        public DEBUG_FILTER_EVENT Code { get; }

        public string Argument { get; }

        public DbgEngineEventFilter(
            int index,
            string name,
            string alias,
            DEBUG_FILTER_EXEC_OPTION execOpt,
            DEBUG_FILTER_CONTINUE_OPTION continueOpt,
            string command = null,
            string argument = null) : base(index, name, alias, execOpt, continueOpt, command)
        {
            Code = (DEBUG_FILTER_EVENT) index;
            Argument = argument;
        }
    }

    public class DbgExceptionEventFilter : DbgEventFilter
    {
        public NTSTATUS Code { get; }

        public string SecondCommand { get; }

        public DbgExceptionEventFilter(
            int index,
            string name,
            string alias,
            DEBUG_FILTER_EXEC_OPTION execOpt,
            DEBUG_FILTER_CONTINUE_OPTION continueOpt,
            NTSTATUS exceptionCode,
            string command = null,
            string secondCommand = null) : base(index, name, alias, execOpt, continueOpt, command)
        {
            Code = exceptionCode;
            SecondCommand = secondCommand;
        }
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public abstract class DbgEventFilter
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

                if (this is DbgExceptionEventFilter e)
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

        protected DbgEventFilter(int index, string name, string alias, DEBUG_FILTER_EXEC_OPTION executionOption, DEBUG_FILTER_CONTINUE_OPTION continueOption, string command)
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
