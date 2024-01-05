using System.Linq;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Metadata;
using ClrDebug;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    [DoNotParallelize]
    public class CordbEngineThreadTests : BaseTest
    {
        #region StackTrace

        [TestMethod]
        public void CordbEngine_Thread_Managed_StackTrace_ManagedFrames_Create()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

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
        public void CordbEngine_Thread_Managed_StackTrace_ManagedFrames_Attach()
        {
            TestDebugAttach(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.First();

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
        public void CordbEngine_Thread_Managed_StackTrace_InternalFrames()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

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
        public void CordbEngine_Thread_Managed_StackTrace_NativeFrames()
        {
            //Look at a thread that was always managed and is now executing inside of native code

            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                ctx =>
                {
                    CompareDbgEngFrames(ctx.CordbEngine.Process.Threads.Single(t => t.IsManaged), ctx.DbgEngEngine.Value);
                },
                useInterop: true
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_NativeToManaged_Bookkeeping_Create()
        {
            //When we launch a native thread and call managed code on it, do we get a managed CreateThread notification?

            TestDebugCreate(
                NativeTestType.Com,
                ctx =>
                {
                    var threads = ctx.CordbEngine.Process.Threads.ToArray();

                    foreach (var thread in threads)
                    {
                        CompareDbgEngFrames(thread, ctx.DbgEngEngine.Value);
                    }
                },
                useInterop: true,
                native: true,
                exeKind: ExeKind.NetFramework
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_NativeToManaged_Bookkeeping_Attach()
        {
            //When a bunch of threads have already started prior to attaching the debugger,
            //do we correctly enumerate all threads and identify which threads are native vs managed?

            TestDebugAttach(
                NativeTestType.Com,
                ctx =>
                {
                    var threads = ctx.CordbEngine.Process.Threads.ToArray();

                    foreach (var thread in threads)
                    {
                        CompareDbgEngFrames(thread, ctx.DbgEngEngine.Value);
                    }
                },
                useInterop: true,
                native: true
            );
        }

        #region Shutdown

        [TestMethod]
        public void CordbEngine_Managed_Detach()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    ctx.CordbEngine.Detach();

                    Assert.IsNull(ctx.CordbEngine.Process);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Managed_Terminate()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    ctx.CordbEngine.Terminate();

                    Assert.IsNull(ctx.CordbEngine.Process);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Interop_Detach()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    AssertEx.Throws<DebugException>(
                        () => ctx.CordbEngine.Detach(),
                        "Error HRESULT CORDBG_E_INTEROP_NOT_SUPPORTED has been returned from a call to a COM component."
                    );
                },
                useInterop: true
            );
        }

        [TestMethod]
        public void CordbEngine_Interop_Terminate()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    ctx.CordbEngine.Terminate();

                    Assert.IsNull(ctx.CordbEngine.Process);
                },
                useInterop: true
            );
        }

        #endregion
        #region Shutdown (Already Terminated)

        [TestMethod]
        public void CordbEngine_Managed_Detach_AlreadyTerminated()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    Process.GetProcessById(ctx.CordbEngine.Process.Id).Kill();

                    ctx.CordbEngine.Detach();

                    Assert.IsNull(ctx.CordbEngine.Process);
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Managed_Terminate_AlreadyTerminated()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    Process.GetProcessById(ctx.CordbEngine.Process.Id).Kill();

                    ctx.CordbEngine.Terminate();

                    Assert.IsNull(ctx.CordbEngine.Process);
                }
            );
        }

        //Interop detach not supported

        [TestMethod]
        public void CordbEngine_Interop_Terminate_AlreadyTerminated()
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    Process.GetProcessById(ctx.CordbEngine.Process.Id).Kill();

                    ctx.CordbEngine.Terminate();

                    Assert.IsNull(ctx.CordbEngine.Process);
                },
                useInterop: true
            );
        }

        #endregion
        #region Main Thread

        [TestMethod]
        public void CordbEngine_Thread_MainThread_ManagedProcess_Managed_Attach()
        {
            TestDebugAttach(
                NativeTestType.Com,
                ctx =>
                {
                    var mainThread = ctx.CordbEngine.Process.Threads.MainThread;

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name == "TestApp.Program.Main"));
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_MainThread_ManagedProcess_Interop_Attach()
        {
            TestDebugAttach(
                NativeTestType.Com,
                ctx =>
                {
                    var mainThread = ctx.CordbEngine.Process.Threads.MainThread;

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name == "TestApp.Program.Main"));
                },
                useInterop: true
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_MainThread_NativeProcess_Managed_Attach()
        {
            TestDebugAttach(
                NativeTestType.Com,
                ctx =>
                {
                    var mainThread = ctx.CordbEngine.Process.Threads.MainThread;

                    //The real main thread will now be a transition frame

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name == "TestApp.Example.Signal"));
                },
                native: true
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_MainThread_NativeProcess_Interop_Attach()
        {
            TestDebugAttach(
                NativeTestType.Com,
                ctx =>
                {
                    var mainThread = ctx.CordbEngine.Process.Threads.MainThread;

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name == "Native.x64!wmain+D6"));
                },
                useInterop: true,
                native: true
            );
        }

        #endregion

        [TestRuntimeMethod]
        public void CordbEngine_Thread_Managed_Type(bool netCore)
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_Type,
                ctx =>
                {
                    var threads = ctx.CordbEngine.Process.Threads.ToArray();

                    Assert.AreEqual(3, threads.Length);

                    Assert.IsNull(threads[0].SpecialType);
                    Assert.AreEqual(TlsThreadTypeFlag.ThreadType_Threadpool_Worker, threads[1].SpecialType);
                    Assert.AreEqual(TlsThreadTypeFlag.ThreadType_Threadpool_Worker, threads[2].SpecialType);
                },
                netCore: netCore
            );
        }

        [TestRuntimeMethod]
        public void CordbEngine_Thread_Native_Type(bool netCore)
        {
            TestDebugCreate(
                TestType.CordbEngine_Thread_Type,
                ctx =>
                {
                    var threads = ctx.CordbEngine.Process.Threads.ToArray();

                    var managedThreads = threads.Where(t => t.IsManaged).ToArray();
                    var nativeThreads = threads.Where(t => !t.IsManaged).ToArray();

                    bool HasThreads(CordbThread[] candidates, TlsThreadTypeFlag type, int count) =>
                        candidates.Count(c => c.SpecialType == type) >= count;

                    Assert.IsTrue(HasThreads(managedThreads, TlsThreadTypeFlag.ThreadType_Threadpool_Worker, 2), "Didn't have threadpool workers");
                    Assert.IsTrue(HasThreads(nativeThreads, TlsThreadTypeFlag.ThreadType_Finalizer, 1), "Didn't have a Finalizer");
                    Assert.IsTrue(HasThreads(nativeThreads, TlsThreadTypeFlag.ThreadType_DbgHelper, 1), "Didn't have DbgHelper");
                },
                netCore: netCore,
                useInterop: true
            );
        }

        private void CompareDbgEngFrames(CordbThread cordbThread, DbgEngEngine dbgEngEngine)
        {
            dbgEngEngine.Invoke(c => c.SystemObjects.CurrentThreadId = c.SystemObjects.GetThreadIdBySystemId(cordbThread.Id));

            //DbgEng just seems to serve up a context it's already in possession of, which not only
            //contains all flags, but also two extra flags that don't seen to be public

            var cordbFrames = cordbThread.StackTrace;
            var dbgEngFrames = dbgEngEngine.GetStackTrace();

            Assert.AreEqual(cordbFrames.Length, dbgEngFrames.Length);

            for (var i = 0; i < cordbFrames.Length; i++)
            {
                var cdbFrame = cordbFrames[i];
                var deFrame = dbgEngFrames[i];

                if (cdbFrame is CordbILFrame || cdbFrame is CordbRuntimeNativeFrame)
                {
                    Assert.AreEqual(deFrame.IP, cdbFrame.Context.IP);
                    Assert.AreEqual(deFrame.SP, cdbFrame.Context.SP);

                    //DbgEng's frames don't store the BP so we can't validate thats
                }
                else
                {
                    //dbgeng!RestrictModNameChars replaces any non alphanumeric characters
                    //in module names with underscores. Pity they didn't consider to treat
                    //periods as valid characters as well!

                    var dbgEngName = deFrame.ToString();

                    var index = dbgEngName.IndexOf('!');

                    if (index != -1)
                    {
                        var chars = dbgEngName.ToCharArray();

                        for (var j = 0; j < index; j++)
                        {
                            if (chars[j] == '_')
                                chars[j] = '.';
                        }

                        dbgEngName = new string(chars);
                    }

                    //Because we now read the filename, we'll get KernelBase.dll
                    //while DbgEng will get KERNELBASE.dll
                    Assert.AreEqual(cdbFrame.ToString(), dbgEngName, true);
                }
            }
        }

        #endregion
    }
}
