using System;

namespace ChaosDbg.Cordb
{
    public class CordbDataBreakpoint : CordbBreakpoint
    {
        public long Address { get; }

        public DR7.Kind Kind { get; }

        public DR7.Length Length { get; }

        private CordbProcess process;

        //The optional thread that this breakpoint applies to. If no thread was specified, it applies to all threads.
        private CordbThread thread;

        protected override void Activate(bool activate)
        {
            /* How to data breakpoints work?
             *
             * x86 processors contain 8 debug registers (DR0-DR8). DR4 and DR5 are obsolete synonyms for DR6 and DR7 respectively,
             * leaving us a total of 6 debug registers to deal with
             *
             * - DR0-DR3 are used to store the addresses that each breakpoint pertains to
             * - DR6 stores information about which breakpoints were hit
             * - DR7 defines the settings of each breakpoint (whether they're for read/write, enabled or not, etc)
             *
             * We define set of special structures "DR6" and "DR7" that we use to model the complex bit flags present in these registers.
             *
             * Data breakpoints are specific to a particular thread. In dbgeng!DataBreakpoint::Insert, if a specific thread
             * has been set, the breakpoint is added to just that thread. Otherwise, it's added to all threads. Amd64MachineInfo::InsertThreadDataBreakpoints
             * is then called multiple times later on to do the work of updating each thread's context
             */

            if (thread != null)
                ActivateForThread(thread, activate);
            else
            {
                foreach (var targetThread in process.Threads)
                    ActivateForThread(targetThread, activate);
            }
        }

        private void ActivateForThread(CordbThread targetThread, bool activate)
        {
            var context = targetThread.RegisterContext;

            context.Dr0 = Address;
            var dr7 = context.Dr7;

            //Clear out all bits but the common control bits
            dr7.ClearBreakpoints();

            dr7.L0 = true;
            dr7.RW0 = Kind;
            dr7.LEN0 = Length;
            dr7.LE = true;

            dr7.Reserved1 = true;

            context.Dr7 = dr7;

            targetThread.TrySaveRegisterContext(); //temp
        }

        public CordbDataBreakpoint(CordbProcess process, long address, DR7.Kind accessKind, DR7.Length size, bool isOneShot) : base(isOneShot)
        {
            if (accessKind == DR7.Kind.IO)
                throw new InvalidOperationException($"Access kind '{accessKind}' is not supported");

            this.process = process;
            Address = address;
            Kind = accessKind;
            Length = size;
        }
    }
}
