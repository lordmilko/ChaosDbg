namespace ChaosDbg.Disasm
{
    public static class WellKnownInterrupt
    {
        //Immediately terminates the calling process with minimum overhead
        public const byte FailFast = 0x29;

        public const byte AssertionFailure = 0x2C;

        public const byte Syscall = 0x2E;

        //Apparently used to send a prompt to the debugger from the debuggee
        public const byte DebuggerPrompt = 0x2D;
    }
}
