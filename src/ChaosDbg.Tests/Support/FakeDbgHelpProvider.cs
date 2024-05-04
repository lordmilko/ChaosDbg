using System;
using System.Threading;
using ChaosLib;

namespace ChaosDbg.Tests
{
    static class FakeDbgHelpProvider
    {
        private static int nextId;

        public static IDbgHelp Acquire()
        {
            var myId = Interlocked.Increment(ref nextId);

            Log.Debug<IDbgHelp>("Creating DbgHelpSession using fake hProcess {hProcess}", myId.ToString("X"));
            var session = (LegacyDbgHelp) DbgHelpProvider.Acquire((IntPtr) myId, invadeProcess: false);
            session.PseudoProcess = true;

            return session;
        }
    }
}
