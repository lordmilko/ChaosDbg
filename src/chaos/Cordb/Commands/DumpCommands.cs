using System;
using System.Linq;
using ChaosDbg;
using ChaosDbg.Cordb;
using ChaosLib.Metadata;

namespace chaos.Cordb.Commands
{
    class DumpCommands : CommandBase
    {
        public DumpCommands(IConsole console, CordbEngineProvider engineProvider) : base(console, engineProvider)
        {
        }

        [Command("da")]
        public void DumpAscii()
        {
            throw new NotImplementedException();
        }

        [Command("db")]
        public void DumpByte()
        {
            throw new NotImplementedException();
        }

        [Command("dc")]
        public void DumpDwordAndChar()
        {
            throw new NotImplementedException();
        }

        [Command("dd")]
        public void DumpDword(
            [Argument] string expr)
        {
            var addr = engine.Process.Evaluator.Evaluate(expr);

            var dumper = new MemoryDumper(engine.Process, Console);

            dumper.Dump(addr, 32);
        }

        [Command("dD")]
        public void DumpDouble()
        {
            throw new NotImplementedException();
        }

        [Command("df")]
        public void DumpFloat()
        {
            throw new NotImplementedException();
        }

        [Command("dg")]
        public void DumpSelector()
        {
            throw new NotImplementedException();
        }

        [Command("dl")]
        public void DumpList()
        {
            throw new NotImplementedException();
        }

        [Command("dq")]
        public void DumpQuad()
        {
            throw new NotImplementedException();
        }

        [Command("ds")]
        public void DumpAsciiString() //STRING or ANSI_STRING structures
        {
            throw new NotImplementedException();
        }

        [Command("dS")]
        public void DumpUnicodeString() //UNICODE_STRING structure
        {
            throw new NotImplementedException();
        }

        [Command("dt")]
        public void DumpType()
        {
            throw new NotImplementedException();
        }

        [Command("du")]
        public void DumpUnicode()
        {
            throw new NotImplementedException();
        }

        [Command("dv")]
        public void DisplayLocals()
        {
            //https://learn.microsoft.com/en-us/windows-hardware/drivers/debuggercmds/dv--display-local-variables-

            var frame = engine.Process.Threads.ActiveThread.EnumerateFrames().First();

            var variables = frame.Variables;

            ((DbgHelpSymbolModule) (variables[0] as CordbNativeVariable).Symbol.Module).SourceTest(frame.Context.IP);

            foreach (var variable in variables)
                Console.WriteLine(variable);
        }

        [Command("dw")]
        public void DumpWord()
        {
            throw new NotImplementedException();
        }

        [Command("dyb")]
        public void DumpBinaryByte()
        {
            throw new NotImplementedException();
        }

        [Command("dyd")]
        public void DumpBinaryDword()
        {
            throw new NotImplementedException();
        }
    }
}
