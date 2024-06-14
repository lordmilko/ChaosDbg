using System;
using System.Collections;
using System.Collections.Generic;
using ChaosDbg.Cordb;
using ClrDebug.DbgEng;

namespace ChaosDbg.DbgEng
{
    public class DbgEngEventFilterStore : IDbgEventFilterStoreInternal, IEnumerable<DbgEngEventFilter>
    {
        private object eventFilterLock = new object();

        private DbgEngSessionInfo session;
        private List<DbgEngEventFilter> filters;

        private Dictionary<string, string> eventFilterAbbreviationMap;

        private Dictionary<string, string> EventFilterAbbreviationMap
        {
            get
            {
                if (eventFilterAbbreviationMap == null)
                    eventFilterAbbreviationMap = CordbEventFilterStore.GetEventFilterMap();

                return eventFilterAbbreviationMap;
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

        private DbgEngEventFilter GetEventFilter(DebugControl control, in DEBUG_SPECIFIC_FILTER_PARAMETERS filter, int index)
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
            EventFilterAbbreviationMap.TryGetValue(text, out var alias);

            var result = new DbgEngEngineEventFilter(index, text, alias, filter, command, argument);

            return result;
        }

        public void Update(DbgEngEventFilter eventFilter)
        {
            var control = session.EngineClient.Control;

            //DbgEng just looks at the execution and continue options, so we can synthesize a fake set of params

            if (eventFilter is DbgEngEngineEventFilter e)
            {
                control.SetSpecificFilterParameters(eventFilter.Index, 1, new[]
                {
                    new DEBUG_SPECIFIC_FILTER_PARAMETERS
                    {
                        ExecutionOption = eventFilter.ExecutionOption,
                        ContinueOption = eventFilter.ContinueOption
                    }
                });

                control.SetSpecificEventFilterArgument(eventFilter.Index, e.Argument);
            }
            else
            {
                var ex = (DbgEngExceptionEventFilter) eventFilter;

                control.SetExceptionFilterParameters(1, new[]
                {
                    new DEBUG_EXCEPTION_FILTER_PARAMETERS
                    {
                        ExceptionCode = ex.Code,
                        ExecutionOption = ex.ExecutionOption,
                        ContinueOption = ex.ContinueOption
                    }
                });

                control.SetExceptionFilterSecondCommand(eventFilter.Index, ex.SecondCommand);
            }

            control.SetEventFilterCommand(eventFilter.Index, eventFilter.Command);
        }

        private DbgEngEventFilter GetExceptionFilter(DebugControl control, in DEBUG_EXCEPTION_FILTER_PARAMETERS filter, int index)
        {
            string command = null;
            string secondCommand = null;

            if (filter.CommandSize > 0)
                command = control.GetEventFilterCommand(index);

            if (filter.SecondCommandSize > 0)
                secondCommand = control.GetExceptionFilterSecondCommand(index);

            var text = control.GetEventFilterText(index);

            EventFilterAbbreviationMap.TryGetValue(text, out var alias);

            var result = new DbgEngExceptionEventFilter(index, text, alias, filter, command, secondCommand);

            return result;
        }

        public IEnumerator<DbgEngEventFilter> GetEnumerator()
        {
            lock (eventFilterLock)
            {
                if (filters != null)
                    return filters.GetEnumerator();

                filters = session.EngineThread.Invoke(() =>
                {
                    var control = session.EngineClient.Control;

                    var num = control.NumberEventFilters;

                    //The exception filters "start" after the specific event filters
                    var eventFilters = control.GetSpecificFilterParameters(0, num.SpecificEvents);
                    var exceptionFilters = control.GetExceptionFilterParameters(num.SpecificExceptions + num.ArbitraryExceptions, null, num.SpecificEvents);

                    var results = new List<DbgEngEventFilter>(eventFilters.Length + exceptionFilters.Length);

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

                return filters.GetEnumerator();
            }
        }

        IEnumerator<IDbgEventFilter> IDbgEventFilterStoreInternal.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
