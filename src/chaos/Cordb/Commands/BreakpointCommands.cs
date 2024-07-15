using System;
using System.Linq;
using ChaosDbg;
using ChaosDbg.Cordb;
using ClrDebug;

namespace chaos.Cordb.Commands
{
    class BreakpointCommands : CommandBase
    {
        public BreakpointCommands(
            IConsole console,
            DebugEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        [Command("bp")]
        public void AddNativeBreakpoint(
            [Argument] string expr)
        {
            var address = engine.Process.Evaluator.Evaluate(expr);

            engine.CreateNativeBreakpoint(address);
        }

        [Command("bpmd")]
        public void AddManagedBreakpoint(
            [Argument] string expr)
        {
            var address = (int) engine.Process.Evaluator.Evaluate(expr);

            var stackTrace = engine.Process.Threads.ActiveThread.StackTrace;
            var topFrame = stackTrace.OfType<CordbILFrame>().FirstOrDefault();

            if (topFrame == null)
                throw new NotImplementedException("Could not find a managed frame to set a breakpoint on");

            var function = topFrame.Function;

            HRESULT hr;

            if ((hr = engine.TryCreateBreakpoint(function, address, out _)) != HRESULT.S_OK)
                Error($"Unable to set breakpoint ({hr}). Some managed offsets disallow setting breakpoints.");
        }

        [Command("bu")]
        public void AddUnresolvedBreakpoint(
            [Argument] string expr)
        {
            throw new NotImplementedException();
        }

        [Command("bm")]
        public void AddPatternBreakpoint()
        {
            throw new NotImplementedException();
        }

        [Command("bd")]
        public void DeleteBreakpoint()
        {
            throw new NotImplementedException();
        }

        [Command("bl")]
        public void ListBreakpoints()
        {
            var breakpoints = engine.Process.Breakpoints;

            foreach (var breakpoint in breakpoints)
                Console.WriteLine(breakpoint);
        }

        [Command("ba")]
        public void AddDataBreakpoint(
            [Argument] DR7.Kind accessKind,
            [Argument] DR7.Length size,
            [Argument] string address)
        {
            var resolvedAddress = engine.Process.Evaluator.Evaluate(address);

            engine.CreateDataBreakpoint(resolvedAddress, accessKind, size);
        }
    }
}
