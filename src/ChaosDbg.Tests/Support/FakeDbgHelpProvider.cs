using System;
using System.Threading;
using ChaosLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    static class FakeDbgHelpProvider
    {
        private static int nextId;

        public static IDbgHelp Acquire(IDbgHelpProvider dbgHelpProvider)
        {
            if (dbgHelpProvider is LegacyDbgHelp)
            {
                var myId = Interlocked.Increment(ref nextId);

                Assert.IsInstanceOfType(dbgHelpProvider, typeof(OutOfProcDbgHelpProvider));

                Log.Debug<IDbgHelp>("Creating DbgHelpSession using fake hProcess {hProcess}", myId.ToString("X"));
                var session = (LegacyDbgHelp) dbgHelpProvider.Acquire((IntPtr) myId, invadeProcess: false);
                session.PseudoProcess = true;

                return session;
            }
            else
            {
                var session = dbgHelpProvider.Acquire(Kernel32.GetCurrentProcess(), invadeProcess: false);

                return session;
            }
        }
    }
}
