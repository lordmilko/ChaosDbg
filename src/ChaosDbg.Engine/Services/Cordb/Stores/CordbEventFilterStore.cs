using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Debugger;
using ClrDebug;
using static ClrDebug.DbgEng.DEBUG_FILTER_EXEC_OPTION;
using static ClrDebug.DbgEng.DEBUG_FILTER_CONTINUE_OPTION;
using static ClrDebug.DbgEng.DEBUG_FILTER_EVENT;
using static ClrDebug.NTSTATUS;

namespace ChaosDbg.Cordb
{
    public class CordbEventFilterStore : IDbgEventFilterStore
    {
        /* DbgEng allows controlling how the debugger reacts in response to exceptions via the use of "event filters"
         * (https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/event-filters)
         *
         * Event filters are stored in the member dbgeng!g_EventFilters. The following command shows how
         * the names of each event filter can be enumerated (assuming dbgeng!g_EventFilters is at 00007ffa`bd1c07b0)
         *
         *    .for (r @$t0=0; @$t0<=29; r $t0=@$t0+1 ) { du poi((00007ffa`bd1c07b0)+(0x70*@$t0)) }
         *
         * You can enumerate all abbreviations, descriptions and their default handling behavior by typing the command
         *
         *     "sx"
         *
         * This command doesn't show the "handled" state for simple debugger events, however this can be found under the Debug -> Event Filters menu in WinDbg
         *
         * DbgEng breaks event filters down into three categories
         * - "Specific event filters", which represent "normal debug events" like process/module/thread load/creation events
         * - "Specific exception filters", which represent exceptions that the debugger knows how to handle
         * - "Arbitrary exception filters", which represent user defined exceptions not known to the debugger
         *
         * There are several methods on IDebugControl that can be used for interacting with event filters
         *
         * | Method                          | Category     | Description                                                                                                                                                                                                          |
         * |---------------------------------|--------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
         * | GetNumberEventFilters           | Common       | Gets the number of "Specific event filters", "Specific exception filters" and "Arbitrary exception filters" known to DbgEng                                                                                          |
         * | GetEventFilterText              | Common       | Retrieves the description of the event filter, e.g. "CLR exception". This is what is enumerated using the .for loop above                                                                                            |
         * | GetEventFilterCommand           | Common       | Gets the command that the debugger should automatically execute whenever the event occurs. In the case of a Specific/arbitrary exception filter, this is the command to execute on a first-chance exception.         |
         * | SetEventFilterCommand           | Common       | Sets the command that the debugger should automatically execute whenever the event occurs. In the case of a Specific/arbitrary exception filter, this is the command to execute on a first-chance exception.         |
         * | GetSpecificFilterParameters     | Debug Events | Retrieves the filter parameters for the "Specific event filters" category                                                                                                                                            |
         * | SetSpecificFilterParameters     | Debug Events | Sets the filter parameters for the "Specific event filters" category                                                                                                                                                 |
         * | GetSpecificFilterArgument       | Debug Events | Gets the argument to use for the given "Specific event filter". e.g. in "sxe ld clr" the argument is "clr". Only certain "Specific event filters" support the use of arguments (see the MSDN link above for details) |
         * | SetSpecificFilterArgument       | Debug Events | Sets the argument to use for the given "Specific event filter". e.g. in "sxe ld clr" the argument is "clr". Only certain "Specific event filters" support the use of arguments (see the MSDN link above for details) |
         * | GetExceptionFilterParameters    | Exceptions   | Retrieves the filter parameters for the "Specific exception filters" and "Arbitrary exception filters" categories                                                                                                    |
         * | SetExceptionFilterParameters    | Exceptions   | Sets the filter parameters for the "Specific exception filters" and "Arbitrary exception filters" categories                                                                                                         |
         * | GetExceptionFilterSecondCommand | Exceptions   | Gets the command to execute for second-chance exceptions for individual filters classified as "Specific exception filters" and "Arbitrary exception filters"                                                         |
         * | SetExceptionFilterSecondCommand | Exceptions   | Sets the command to execute for second-chance exceptions for individual filters classified as "Specific exception filters" and "Arbitrary exception filters"                                                         |
         *
         * DbgEng's DEBUG_FILTER* enum values map to display strings as follows
         *
         * - DEBUG_FILTER_GO_HANDLED          -> handled
         * - DEBUG_FILTER_GO_NOT_HANDLED      ->not handled
         *
         * - DEBUG_FILTER_BREAK               -> break
         * - DEBUG_FILTER_SECOND_CHANCE_BREAK -> second-chance break
         * - DEBUG_FILTER_OUTPUT              -> output
         * - DEBUG_FILTER_IGNORE              -> ignore
         */

        private object eventFilterLock = new object();

        private DbgEventFilter[] filters =
        {
            //Debugger Events

            new DbgEngineEventFilter(index: (int) CREATE_THREAD,       name: "Create thread",                                  alias: "ct",   execOpt: IGNORE,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) EXIT_THREAD,         name: "Exit thread",                                    alias: "et",   execOpt: IGNORE,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) CREATE_PROCESS,      name: "Create process",                                 alias: "cpr",  execOpt: IGNORE,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) EXIT_PROCESS,        name: "Exit process",                                   alias: "epr",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) LOAD_MODULE,         name: "Load module",                                    alias: "ld",   execOpt: OUTPUT,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) UNLOAD_MODULE,       name: "Unload module",                                  alias: "ud",   execOpt: IGNORE,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) SYSTEM_ERROR,        name: "System error",                                   alias: "ser",  execOpt: IGNORE,              continueOpt: GO_NOT_HANDLED),
            new DbgEngineEventFilter(index: (int) INITIAL_BREAKPOINT,  name: "Initial breakpoint",                             alias: "ibp",  execOpt: BREAK,               continueOpt: GO_HANDLED),
            new DbgEngineEventFilter(index: (int) INITIAL_MODULE_LOAD, name: "Initial module load",                            alias: "iml",  execOpt: IGNORE,              continueOpt: GO_HANDLED),
            new DbgEngineEventFilter(index: (int) DEBUGGEE_OUTPUT,     name: "Debuggee output",                                alias: "out",  execOpt: OUTPUT,              continueOpt: GO_HANDLED),

            //Exceptions

            new DbgExceptionEventFilter(index: 10, name: "Access violation",                               alias: "av",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_ACCESS_VIOLATION),
            new DbgExceptionEventFilter(index: 11, name: "Assertion failure",                              alias: "asrt", execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_ASSERTION_FAILURE),
            new DbgExceptionEventFilter(index: 12, name: "Application hang",                               alias: "aph",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_APPLICATION_HANG),
            new DbgExceptionEventFilter(index: 13, name: "Break instruction exception",                    alias: "bpe",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: STATUS_BREAKPOINT),
            //new CordbEventFilter(name: "Break instruction exception continue",           alias: "bpec", execOpt: GO_HANDLED),
            new DbgExceptionEventFilter(index: 14, name: "C++ EH exception",                               alias: "eh",   execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_CPP_EH_EXCEPTION),
            new DbgExceptionEventFilter(index: 15, name: "CLR exception",                                  alias: "clr",  execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_CLR_EXCEPTION),
            new DbgExceptionEventFilter(index: 16, name: "CLR notification exception",                     alias: "clrn", execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_HANDLED,     exceptionCode: CLRDATA_NOTIFY_EXCEPTION),
            new DbgExceptionEventFilter(index: 17, name: "Control-Break exception",                        alias: "cce",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: DBG_CONTROL_BREAK),
            //new CordbEventFilter(name: "Control-Break exception continue",               alias: "cc", execOpt: GO_HANDLED),
            new DbgExceptionEventFilter(index: 18, name: "Control-C exception",                            alias: "cce",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: DBG_CONTROL_C),
            //new CordbEventFilter(name: "Control-C exception continue",                   alias: "cc", execOpt: GO_HANDLED),
            new DbgExceptionEventFilter(index: 19, name: "Data misaligned",                                alias: "dm",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_DATATYPE_MISALIGNMENT),
            new DbgExceptionEventFilter(index: 20, name: "Debugger command exception",                     alias: "dbce", execOpt: IGNORE,              continueOpt: GO_HANDLED,     exceptionCode: DBG_COMMAND_EXCEPTION),
            new DbgExceptionEventFilter(index: 21, name: "Guard page violation",                           alias: "gp",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_GUARD_PAGE_VIOLATION),
            new DbgExceptionEventFilter(index: 22, name: "Illegal instruction",                            alias: "ii",   execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_ILLEGAL_INSTRUCTION),
            new DbgExceptionEventFilter(index: 23, name: "In-page I/O error",                              alias: "ip",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_IN_PAGE_ERROR),
            new DbgExceptionEventFilter(index: 24, name: "Integer divide-by-zero",                         alias: "dz",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_INTEGER_DIVIDE_BY_ZERO),
            new DbgExceptionEventFilter(index: 25, name: "Integer overflow",                               alias: "iov",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_INTEGER_OVERFLOW),
            new DbgExceptionEventFilter(index: 26, name: "Invalid handle",                                 alias: "ch",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_INVALID_HANDLE),
            //new CordbEventFilter(name: "Invalid handle continue",                        alias: "hc", execOpt: GO_NOT_HANDLED),
            new DbgExceptionEventFilter(index: 27, name: "Invalid lock sequence",                          alias: "lsq",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_INVALID_LOCK_SEQUENCE),
            new DbgExceptionEventFilter(index: 28, name: "Invalid system call",                            alias: "isc",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_INVALID_SYSTEM_SERVICE),
            new DbgExceptionEventFilter(index: 29, name: "Port disconnected",                              alias: "3c",   execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_PORT_DISCONNECTED),
            new DbgExceptionEventFilter(index: 30, name: "Service hang",                                   alias: "svh",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_SERVICE_HANG),
            new DbgExceptionEventFilter(index: 31, name: "Single step exception",                          alias: "sse",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: STATUS_SINGLE_STEP),
            //new CordbEventFilter(name: "Single step exception continue",                 alias: "ssec", execOpt: GO_HANDLED),
            new DbgExceptionEventFilter(index: 32, name: "Security check failure or stack buffer overrun", alias: "sbo",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_STACK_BUFFER_OVERRUN),
            new DbgExceptionEventFilter(index: 33, name: "Stack overflow",                                 alias: "sov",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_STACK_OVERFLOW),
            new DbgExceptionEventFilter(index: 34, name: "Verifier stop",                                  alias: "vs",   execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_VERIFIER_STOP),
            new DbgExceptionEventFilter(index: 35, name: "Visual C++ exception",                           alias: "vcpp", execOpt: IGNORE,              continueOpt: GO_HANDLED,     exceptionCode: STATUS_VCPP_EXCEPTION),
            new DbgExceptionEventFilter(index: 36, name: "Wake debugger",                                  alias: "wkd",  execOpt: BREAK,               continueOpt: GO_NOT_HANDLED, exceptionCode: STATUS_WAKE_SYSTEM_DEBUGGER),
            new DbgExceptionEventFilter(index: 37, name: "Windows Runtime Originate Error",                alias: "rto",  execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: (NTSTATUS) 0x40080201), //Unknown name
            new DbgExceptionEventFilter(index: 38, name: "Windows Runtime Transform Error",                alias: "rtt",  execOpt: SECOND_CHANCE_BREAK, continueOpt: GO_NOT_HANDLED, exceptionCode: (NTSTATUS) 0x40080202), //Unknown name
            new DbgExceptionEventFilter(index: 39, name: "WOW64 breakpoint",                               alias: "wob",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: STATUS_WX86_BREAKPOINT),
            new DbgExceptionEventFilter(index: 40, name: "WOW64 single step exception",                    alias: "wos",  execOpt: BREAK,               continueOpt: GO_HANDLED,     exceptionCode: STATUS_WX86_SINGLE_STEP)
        };

        public void SetArgument(WellKnownEventFilter kind, string argumentValue)
        {
            throw new NotImplementedException();
        }

        public DbgEventFilter this[WellKnownEventFilter kind]
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public IEnumerator<DbgEventFilter> GetEnumerator()
        {
            lock (eventFilterLock)
            {
                //Make a copy so they don't trip over us modifying the original collection
                return ((IEnumerable<DbgEventFilter>) filters.ToArray()).GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public static Dictionary<string, string> GetEventFilterMap()
        {
            return new CordbEventFilterStore().filters.ToDictionary(v => v.Name, v => v.Alias);
        }
    }
}
