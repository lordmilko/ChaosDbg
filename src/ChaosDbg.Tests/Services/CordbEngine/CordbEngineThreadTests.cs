using System.Linq;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public class CordbEngineThreadTests : BaseTest
    {
        #region StackTrace

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_ManagedFrames()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                engine =>
                {
                    var thread = engine.Process.Threads.Single();

                    thread.Verify().StackTrace(
                        "Transition Frame",
                        "System.Threading.Thread.Sleep",
                        "TestApp.Program.SignalReady",
                        "TestApp.CordbEngine_Thread_StackTrace.Managed",
                        "TestApp.Program.Main",
                        "Transition Frame"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_InternalFrames()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                engine =>
                {
                    var thread = engine.Process.Threads.Single();

                    thread.Verify().StackTrace(
                        "Transition Frame",
                        "System.Threading.Thread.Sleep",
                        "TestApp.Program.SignalReady",
                        "<>c.<Internal>b__1_0",
                        "[Runtime]",
                        "Transition Frame",
                        "[Runtime]",
                        "TestApp.CordbEngine_Thread_StackTrace.Internal",
                        "TestApp.Program.Main",
                        "Transition Frame"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_StackTrace_NativeFrames()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                engine =>
                {
                    var cordbFrames = engine.Process.Threads.Single().StackTrace;
                    var dbgEngFrames = GetDbgEngFrames(engine);

                    Assert.AreEqual(cordbFrames.Length, dbgEngFrames.Length);

                    for (var i = 0; i < cordbFrames.Length; i++)
                    {
                        var cdbFrame = cordbFrames[i];
                        var deFrame = dbgEngFrames[i];

                        if (cdbFrame is CordbILFrame f)
                        {
                            Assert.AreEqual(deFrame.IP, f.Context.IP);
                            Assert.AreEqual(deFrame.SP, f.Context.SP);

                            //DbgEng's frames don't store the BP so we can't validate thats
                        }
                        else
                            Assert.AreEqual(cdbFrame.ToString(), deFrame.ToString());
                    }
                },
                useInterop: true
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_Type()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_Type,
                engine =>
                {
                    var threads = engine.Process.Threads.ToArray();

                    Assert.AreEqual(3, threads.Length);

                    Assert.AreEqual((TlsThreadTypeFlag) 0, threads[0].Type);
                    Assert.AreEqual(TlsThreadTypeFlag.ThreadType_Threadpool_Worker, threads[1].Type);
                    Assert.AreEqual(TlsThreadTypeFlag.ThreadType_Threadpool_Worker, threads[2].Type);
                }
            );
        }

        private DbgEngFrame[] GetDbgEngFrames(CordbEngine engine)
        {
            using var dbgEngEngine = GetService<DbgEngEngine>();

            dbgEngEngine.Attach(engine.Process.Id, true);
            dbgEngEngine.WaitForBreak();

            var frames = dbgEngEngine.GetStackTrace();

            return frames;
        }

        #endregion
    }
}
