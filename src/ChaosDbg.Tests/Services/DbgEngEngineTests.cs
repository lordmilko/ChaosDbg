using System;
using System.Diagnostics;
using System.Threading;
using ChaosDbg.DbgEng;
using ChaosDbg.Debugger;
using ClrDebug.DbgEng;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class DbgEngEngineTests : BaseTest
    {
        [TestMethod]
        public void DbgEngEngine_Launch_NativeOnPath()
        {
            var engineProvider = GetService<DebugEngineProvider>();

            engineProvider.EngineFailure += (s, e) => Debug.Assert(false);

            using var engine = engineProvider.DbgEng.CreateProcess("notepad", true);

            var cts = new CancellationTokenSource();
            cts.CancelAfter(10000);

            engine.WaitForBreak(cts.Token);
            cts.Token.ThrowIfCancellationRequested();
        }

        [TestMethod]
        public void DbgEngEngine_DebugChildProcesses()
        {
            Test((engineProvider, path) =>
            {
                using var parentEventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName);
                using var childEventHandle = new EventWaitHandle(false, EventResetMode.ManualReset, $"{EventName}_Child");

                //I think each process would want to trigger an initial break, so just tell them all to run without breaking
                using var engine = engineProvider.DbgEng.CreateProcess($"{path} {TestType.DbgEngEngine_ChildProcess} {EventName}", initialBreak: false, debugChildProcesses: true);

                if (!parentEventHandle.WaitOne(10000))
                    throw new TimeoutException();

                if (!childEventHandle.WaitOne(10000))
                    throw new TimeoutException();
            });
        }

        [TestMethod]
        public void DbgEngEngine_ProcessStore_ActiveProcessChangesOnBreak()
        {
            Test((engineProvider, path) =>
            {
                engineProvider.EngineInitialized += (s, e) =>
                {
                    /* DbgEng uses a function dbgeng!MatchFilterPath to match the specified string. However,
                     * it erroneously matches any module that "starts with" the specified module name. Thus, if we break
                     * on "clr", we'll hit both clr and clrjit. For any module name containing a wildcard character,
                     * dbgeng!MatchFilterPath defers to dbghelp!SymMatchStringW. We work around this by explicitly doing ".dll" */
                    ((DbgEngEngine) s).Session.EventFilters.SetEventFilter(WellKnownEventFilter.LoadModule, DEBUG_FILTER_EXEC_OPTION.BREAK, argument: "clr.dll");
                };

                //I think each process would want to trigger an initial break, so just tell them all to run without breaking
                using var engine = engineProvider.DbgEng.CreateProcess($"{path} {TestType.DbgEngEngine_ChildProcess} {EventName}", initialBreak: false, debugChildProcesses: true);

                engine.WaitForBreak();
                Assert.AreEqual(2, engine.Session.Processes.Count);
                var firstActive = engine.ActiveProcess;

                engine.Continue();

                engine.WaitForBreak();
                Assert.AreEqual(4, engine.Session.Processes.Count);
                var secondActive = engine.ActiveProcess;

                Assert.AreNotEqual(firstActive, secondActive);
            });
        }

        private void Test(Action<DebugEngineProvider, string> action)
        {
            var engineProvider = GetService<DebugEngineProvider>();
            engineProvider.EngineFailure += (s, e) => Debug.Assert(false);

            var path = GetTestAppPath(true, false);

            action(engineProvider, path);
        }
    }
}
