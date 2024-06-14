using ClrDebug.DbgEng;

namespace ChaosDbg.Cordb
{
    public class CordbEventFilter : IDbgEventFilter
    {
        public string Name { get; }
        public string Alias { get; }
        public DEBUG_FILTER_EXEC_OPTION ExecutionOption { get; }
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption { get; }

        public CordbEventFilter(string name, string alias, DEBUG_FILTER_EXEC_OPTION execOpt, DEBUG_FILTER_CONTINUE_OPTION continueOpt)
        {
            Name = name;
            Alias = alias;
            ExecutionOption = execOpt;
            ContinueOption = continueOpt;
        }
    }
}
