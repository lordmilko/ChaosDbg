using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using ChaosDbg.Cordb;
using ChaosDbg.DbgEng;
using ChaosDbg.Metadata;
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
        public void CordbEngine_Thread_Managed_StackTrace_ManagedFrames_Create()
        {
            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_ManagedFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

                    thread.Verify().StackTrace(
                        "Transition Frame",
                        "mscorlib.dll!System.Threading.Thread.Sleep(int millisecondsTimeout)",
                        "Managed.x64.exe!TestApp.Program.SignalReady()",
                        "Managed.x64.exe!TestApp.CordbEngine_Thread_StackTrace.Managed()",
                        "Managed.x64.exe!TestApp.Program.Main(string[] args)",
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
                        "mscorlib.dll!System.Threading.Thread.Sleep(int millisecondsTimeout)",
                        "Managed.x64.exe!TestApp.Program.SignalReady()",
                        "Managed.x64.exe!TestApp.CordbEngine_Thread_StackTrace.Managed()",
                        "Managed.x64.exe!TestApp.Program.Main(string[] args)",
                        "Transition Frame"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_Managed_StackTrace_InternalFrames()
        {
            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                ctx =>
                {
                    var thread = ctx.CordbEngine.Process.Threads.Single();

                    thread.Verify().StackTrace(
                        "Transition Frame",
                        "mscorlib.dll!System.Threading.Thread.Sleep(int millisecondsTimeout)",
                        "Managed.x64.exe!TestApp.Program.SignalReady()",
                        "Managed.x64.exe!TestApp.CordbEngine_Thread_StackTrace+<>c.<Internal>b__1_0(IntPtr a, IntPtr b)",
                        "DomainBoundILStubClass.IL_STUB_ReversePInvoke(Int64, Int64)",
                        "Transition Frame",
                        "DomainBoundILStubClass.IL_STUB_PInvoke(TestApp.EnumWindowsProc, IntPtr)",
                        "Managed.x64.exe!TestApp.CordbEngine_Thread_StackTrace.Internal()",
                        "Managed.x64.exe!TestApp.Program.Main(string[] args)",
                        "Transition Frame"
                    );
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_Managed_StackTrace_NativeFrames()
        {
            //Look at a thread that was always managed and is now executing inside of native code

            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                ctx =>
                {
                    CompareDbgEngFrames(ctx.CordbEngine.Process.Threads.Single(t => t.IsManaged), ctx.InProcDbgEng.Value);
                },
                useInterop: true
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_Managed_StackTrace_NativeFrames_AtNativeException()
        {
            //We're on the Win32 Event Thread and try and do a native stack trace

            var endEvent = new ManualResetEventSlim(false);

            TestContext context = null;

            TestSignalledDebugCreate(
                TestType.CordbEngine_Thread_StackTrace_InternalFrames,
                ctx =>
                {
                    context = ctx;

                    //LoaderBP
                    ctx.WaitForBreakpoint();
                    ctx.CordbEngine.Continue();

                    //Exception
                    ctx.WaitForBreakpoint();

                    Assert.IsInstanceOfType(ctx.Process.Session.EventHistory.LastStopReason, typeof(CordbNativeFirstChanceExceptionPauseReason));

                    if (!endEvent.Wait(10000))
                        throw new TimeoutException();
                },
                useInterop: true,
                customExe: "powershell",
                waitForSignal: false,
                hookEvents: p => p.ExceptionHit += (s, e) =>
                {
                    Assert.AreEqual(NTSTATUS.STATUS_CPP_EH_EXCEPTION, e.Exception.ExceptionRecord.ExceptionCode);

                    try
                    {
                        CompareDbgEngFrames(context.ActiveThread, context.InProcDbgEng.Value);
                    }
                    finally
                    {
                        endEvent.Set();
                    }
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Thread_NativeToManaged_Bookkeeping_Create()
        {
            //When we launch a native thread and call managed code on it, do we get a managed CreateThread notification?

            TestSignalledDebugCreate(
                NativeTestType.Com,
                ctx =>
                {
                    var threads = ctx.CordbEngine.Process.Threads.ToArray();

                    foreach (var thread in threads)
                    {
                        CompareDbgEngFrames(thread, ctx.InProcDbgEng.Value);
                    }
                },
                useInterop: true,
                nativeTestApp: true,
                frameworkKind: FrameworkKind.NetFramework
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
                        CompareDbgEngFrames(thread, ctx.InProcDbgEng.Value);
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name.Contains("TestApp.Program.Main")));
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

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name.Contains("TestApp.Program.Main")));
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

                    //The real main thread will now be a transition frame. It's not very useful, but that's not our problem;
                    //the main thread is the main thread
                    Assert.AreEqual("Transition Frame", mainThread.StackTrace.Single().Name);
                    Assert.IsTrue(!mainThread.StackTrace.Any(f => f.Name.Contains("TestApp.Example.Signal")));
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

                    Assert.IsTrue(mainThread.StackTrace.Any(f => f.Name.StartsWith("Native.x86!wmain+") || f.Name.StartsWith("Native.x64!wmain+")));
                },
                useInterop: true,
                native: true
            );
        }

        #endregion

        [TestRuntimeMethod]
        public void CordbEngine_Thread_Managed_Type(bool netCore)
        {
            TestSignalledDebugCreate(
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
            TestSignalledDebugCreate(
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

        private unsafe void CompareDbgEngFrames(CordbThread cordbThread, DbgEngEngine dbgEngEngine)
        {
            dbgEngEngine.Invoke(c => c.SystemObjects.CurrentThreadId = c.SystemObjects.GetThreadIdBySystemId(cordbThread.Id));

            //DbgEng just seems to serve up a context it's already in possession of, which not only
            //contains all flags, but also two extra flags that don't seen to be public

            var cordbFrames = cordbThread.StackTrace;
            var dbgEngFrames = dbgEngEngine.GetStackTrace();

            //Validate that our crazy DbgEng detours haven't broken the normal DbgEng stack tracing logic
            var lastDbgEngFrame = dbgEngFrames.Last();
            Assert.IsTrue(lastDbgEngFrame.ToString().Contains("RtlUserThreadStart"));

            Debug.WriteLine($"DbgEng: {dbgEngFrames.Length}, Cordb: {cordbFrames.Length}");

            Assert.AreEqual(cordbFrames.Length, dbgEngFrames.Length);

            for (var i = 0; i < cordbFrames.Length; i++)
            {
                var cdbFrame = cordbFrames[i];
                var deFrame = dbgEngFrames[i];

                if (cdbFrame is CordbILFrame || cdbFrame is CordbRuntimeNativeFrame || deFrame.Name == null) //It seems that even with Cordb's NativeFrame, we don't set the Name to be the address, so it seems acceptable for DbgEng to do the same thing
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

                    var cdbName = cdbFrame.ToString();
                    var dbgEngName = deFrame.ToString();

                    var dbgEngNameStart = dbgEngName.IndexOf('!');

                    if (dbgEngNameStart != -1)
                    {
                        //DbgHelp seems to trim leading underscores from certain symbol names.
                        //We display the original symbol name reported by DIA

                        var cdbNameStart = cdbName.IndexOf('!');

                        var chars = dbgEngName.ToCharArray().ToList();

                        if (cdbNameStart != -1 && cdbName[cdbNameStart + 1] == '_' && dbgEngName[dbgEngNameStart + 1] != '_')
                            chars.Insert(dbgEngNameStart + 1, '_');

                        for (var j = 0; j < dbgEngNameStart; j++)
                        {
                            if (chars[j] == '_')
                            {
                                //If the name actually is meant to contain an underscore, e.g. vcruntime140_clr0400.dll,
                                //messing with the name will introduce an issue
                                if (j < cdbName.Length && cdbName[j] == '_')
                                    continue;

                                chars[j] = '.';
                            }
                        }

                        dbgEngName = new string(chars.ToArray());
                    }

                    //Because we our native module name reader reads the filename instead of the in-memory name (which can contain faulty information. See CordbNativeModule.GetNativeModuleName for details), we'll get KernelBase.dll
                    //while DbgEng will get KERNELBASE.dll
                    Assert.AreEqual(cdbName, dbgEngName, ignoreCase: true);
                }
            }
        }

        #endregion
    }
}
