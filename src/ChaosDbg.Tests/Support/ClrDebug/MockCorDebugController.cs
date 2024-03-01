using System;
using ClrDebug;

namespace ChaosDbg.Tests
{
    abstract class MockCorDebugController : ICorDebugController
    {
        public HRESULT Stop(int dwTimeoutIgnored)
        {
            throw new NotImplementedException();
        }

        public HRESULT Continue(bool fIsOutOfBand)
        {
            throw new NotImplementedException();
        }

        public HRESULT IsRunning(out bool pbRunning)
        {
            throw new NotImplementedException();
        }

        public HRESULT HasQueuedCallbacks(ICorDebugThread pThread, out bool pbQueued)
        {
            throw new NotImplementedException();
        }

        public HRESULT EnumerateThreads(out ICorDebugThreadEnum ppThreads)
        {
            throw new NotImplementedException();
        }

        public HRESULT SetAllThreadsDebugState(CorDebugThreadState state, ICorDebugThread pExceptThisThread)
        {
            throw new NotImplementedException();
        }

        public HRESULT Detach()
        {
            throw new NotImplementedException();
        }

        public HRESULT Terminate(int exitCode)
        {
            throw new NotImplementedException();
        }

        public HRESULT CanCommitChanges(int cSnapshots, ref ICorDebugEditAndContinueSnapshot pSnapshots,
            out ICorDebugErrorInfoEnum pError)
        {
            throw new NotImplementedException();
        }

        public HRESULT CommitChanges(int cSnapshots, ref ICorDebugEditAndContinueSnapshot pSnapshots,
            out ICorDebugErrorInfoEnum pError)
        {
            throw new NotImplementedException();
        }
    }
}
