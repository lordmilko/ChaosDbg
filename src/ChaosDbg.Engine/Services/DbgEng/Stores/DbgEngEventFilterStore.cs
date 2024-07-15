using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Cordb;
using ChaosDbg.Debugger;
using ChaosLib;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    public class DbgEngEventFilterStore : IDbgEventFilterStore
    {
        private object eventFilterLock = new object();

        private DbgEngSessionInfo session;
        private List<DbgEventFilter> filters;

        private Dictionary<string, string> nameToAliasMap;

        private Dictionary<string, string> NameToAliasMap
        {
            get
            {
                if (nameToAliasMap == null)
                    nameToAliasMap = CordbEventFilterStore.GetEventFilterMap();

                return nameToAliasMap;
            }
        }

        private Dictionary<WellKnownEventFilter, string> wellKnownEventFilterToAliasMap;

        private Dictionary<WellKnownEventFilter, string> WellKnownEventFilterToAliasMap
        {
            get
            {
                if (wellKnownEventFilterToAliasMap == null)
                    wellKnownEventFilterToAliasMap = Enum.GetValues(typeof(WellKnownEventFilter)).Cast<WellKnownEventFilter>().ToDictionary(v => v, v => v.GetDescription());

                return wellKnownEventFilterToAliasMap;
            }
        }

        public DbgEngEventFilterStore(DbgEngSessionInfo session)
        {
            this.session = session;
        }

        public void Refresh(int index)
        {
            lock (eventFilterLock)
            {
                if (filters == null)
                    return;

                if (index >= filters.Count)
                    filters = null; //Don't know what's going on; clear out all filters as a fallback measure
                else
                {
                    filters[index] = session.EngineThread.Invoke(() =>
                    {
                        var control = session.EngineClient.Control;

                        var num = control.NumberEventFilters;

                        if (index < num.SpecificEvents)
                        {
                            var eventFilter = control.GetSpecificFilterParameters(index, 1);
                            return GetEventFilter(control, eventFilter[0], index);
                        }
                        else
                        {
                            var eventFilter = control.GetExceptionFilterParameters(index, null, 1);
                            return GetExceptionFilter(control, eventFilter[0], index);
                        }
                    });
                }
            }
        }

        public void Clear()
        {
            lock (eventFilterLock)
                filters = null;
        }

        private DbgEventFilter GetEventFilter(DebugControl control, in DEBUG_SPECIFIC_FILTER_PARAMETERS filter, int index)
        {
            string command = null;
            string argument = null;

            if (filter.CommandSize > 0)
                command = control.GetEventFilterCommand(index);

            var text = control.GetEventFilterText(index);

            //Size includes \0
            if (filter.ArgumentSize > 1)
                argument = control.GetSpecificEventFilterArgument(index);

            //There's no way to get the abbreviated name from DbgEng. Thus, the best we can do is use pre-recorded information
            NameToAliasMap.TryGetValue(text, out var alias);

            var result = new DbgEngineEventFilter(index, text, alias, filter.ExecutionOption, filter.ContinueOption, command, argument);

            return result;
        }

        public void SetEventFilter(
            DbgEngineEventFilter eventFilter,
            DEBUG_FILTER_EXEC_OPTION? execOption = null,
            DEBUG_FILTER_CONTINUE_OPTION? continueOption = null,
            string argument = null,
            string command = null)
        {
            session.EngineThread.Invoke(() =>
            {
                var control = session.EngineClient.Control;

                if (execOption != null || continueOption != null)
                {
                    control.SetSpecificFilterParameters(eventFilter.Index, 1, new[]
                    {
                        //DbgEng just looks at the execution and continue options, so we can synthesize a fake set of params

                        new DEBUG_SPECIFIC_FILTER_PARAMETERS
                        {
                            ExecutionOption = execOption ?? eventFilter.ExecutionOption,
                            ContinueOption = continueOption ?? eventFilter.ContinueOption
                        }
                    });
                }

                //We interpret a null argument as being "not specified", and string.Empty as "remove the argument".
                //DbgEng however treats null as being "remove the argument", so we must transform it
                if (argument != null)
                    control.SetSpecificEventFilterArgument(eventFilter.Index, argument == string.Empty ? null : argument);
            });
        }

        public void SetEventFilter(
            DbgExceptionEventFilter eventFilter,
            DEBUG_FILTER_EXEC_OPTION? execOption = null,
            DEBUG_FILTER_CONTINUE_OPTION? continueOption = null,
            string command = null,
            string secondCommand = null)
        {
            session.EngineThread.Invoke(() =>
            {
                var control = session.EngineClient.Control;

                control.SetExceptionFilterParameters(1, new[]
                {
                    new DEBUG_EXCEPTION_FILTER_PARAMETERS
                    {
                        ExceptionCode = eventFilter.Code,
                        ExecutionOption = execOption ?? eventFilter.ExecutionOption,
                        ContinueOption = continueOption ?? eventFilter.ContinueOption
                    }
                });

                if (command != null)
                    control.SetEventFilterCommand(eventFilter.Index, command == string.Empty ? null : command);

                if (secondCommand != null)
                    control.SetExceptionFilterSecondCommand(eventFilter.Index, secondCommand);
            });
        }

        private DbgEventFilter GetExceptionFilter(DebugControl control, in DEBUG_EXCEPTION_FILTER_PARAMETERS filter, int index)
        {
            string command = null;
            string secondCommand = null;

            if (filter.CommandSize > 0)
                command = control.GetEventFilterCommand(index);

            if (filter.SecondCommandSize > 0)
                secondCommand = control.GetExceptionFilterSecondCommand(index);

            var text = control.GetEventFilterText(index);

            NameToAliasMap.TryGetValue(text, out var alias);

            var result = new DbgExceptionEventFilter(index, text, alias, filter.ExecutionOption, filter.ContinueOption, filter.ExceptionCode, command, secondCommand);

            return result;
        }

        public void SetArgument(WellKnownEventFilter kind, string argumentValue)
        {
            //This method will call the ChangeEngineState() event callback, which will dispatch to our Refresh() method and
            //create a new event filter object before this method returns
            session.EngineThread.Invoke(() =>
            {
                var eventFilter = this[kind];
                session.EngineClient.Control.SetSpecificEventFilterArgument(eventFilter.Index, argumentValue);
            });
        }

        public DbgEventFilter this[WellKnownEventFilter kind]
        {
            get
            {
                //Considering how rare it might be to modify an event filter, I'm not sure it's worth the memory overhead
                //of storing a map
                var alias = WellKnownEventFilterToAliasMap[kind];

                lock (eventFilterLock)
                {
                    EnsureFiltersNoLock();

                    foreach (var filter in filters)
                    {
                        if (filter.Alias == alias)
                            return filter;
                    }
                }

                throw new InvalidOperationException($"Could not find a filter of type {kind}");
            }
        }

        public IEnumerator<DbgEventFilter> GetEnumerator()
        {
            lock (eventFilterLock)
            {
                EnsureFiltersNoLock();

                //Make a copy so they don't trip over us modifying the original collection
                return ((IEnumerable<DbgEventFilter>) filters.ToArray()).GetEnumerator();
            }
        }

        private void EnsureFiltersNoLock()
        {
            if (filters != null)
                return;

            filters = session.EngineThread.Invoke(() =>
            {
                var control = session.EngineClient.Control;

                var num = control.NumberEventFilters;

                //The exception filters "start" after the specific event filters
                var eventFilters = control.GetSpecificFilterParameters(0, num.SpecificEvents);
                var exceptionFilters = control.GetExceptionFilterParameters(num.SpecificExceptions + num.ArbitraryExceptions, null, num.SpecificEvents);

                var results = new List<DbgEventFilter>(eventFilters.Length + exceptionFilters.Length);

                for (var i = 0; i < eventFilters.Length; i++)
                {
                    var filter = eventFilters[i];

                    results.Add(GetEventFilter(control, filter, i));
                }

                for (var i = 0; i < exceptionFilters.Length; i++)
                {
                    var filter = exceptionFilters[i];
                    var index = i + num.SpecificEvents;

                    results.Add(GetExceptionFilter(control, filter, index));
                }

                //The NTSTATUS value names of Windows Runtime Originate Error and Windows Runtime Transform Error are unknown, even to DbgShell
                return results;
            });
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
