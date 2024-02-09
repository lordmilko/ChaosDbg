using System;
using ChaosLib;

namespace ChaosDbg.Tests
{
    static class DbgHelpProvider
    {
        private static int nextId;

        public static DbgHelpSession Acquire()
        {
            nextId++;

            var session = new DbgHelpSession((IntPtr) nextId, invadeProcess: false);
            session.PseudoProcess = true;

            return session;
        }
    }
}
