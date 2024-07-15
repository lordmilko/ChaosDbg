using System.ComponentModel;

namespace ChaosDbg.Debugger
{
    public enum WellKnownEventFilter
    {
        #region Debugger Events

        [Description("ct")]
        CreateThread,

        [Description("et")]
        ExitThread,

        [Description("cpr")]
        CreateProcess,

        [Description("epr")]
        ExitProcess,

        [Description("ld")]
        LoadModule,

        [Description("ud")]
        UnloadModule,

        [Description("ser")]
        SystemError,

        [Description("ipb")]
        InitialBreakpoint,

        [Description("iml")]
        InitialModuleLoad,

        [Description("out")]
        DebuggeeOutput,

        #endregion
        #region Exceptions

        [Description("av")]
        AccessViolation,

        [Description("asrt")]
        AssertionFailure,

        [Description("aph")]
        ApplicationHang,

        [Description("bpe")]
        BreakInstructionException,

        [Description("eh")]
        CppEHException,

        [Description("clr")]
        CLRException,

        [Description("clrn")]
        CLRNotificationException,

        [Description("cce")]
        ControlBreakException,

        [Description("cce")]
        ControlCException,

        [Description("dm")]
        DataMisaligned,

        [Description("dbce")]
        DebuggerCommandException,

        [Description("gp")]
        GuardPageViolation,

        [Description("ii")]
        IllegalInstruction,

        [Description("ip")]
        InPageIOError,

        [Description("dz")]
        IntegerDivideByZero,

        [Description("iov")]
        IntegerOverflow,

        [Description("ch")]
        InvalidHandle,

        [Description("lsq")]
        InvalidLockSequence,

        [Description("isc")]
        InvalidSystemCall,

        [Description("3c")]
        PortDisconnected,

        [Description("svh")]
        ServiceHang,

        [Description("sse")]
        SingleStepException,

        [Description("sbo")]
        SecurityCheckFailureOrStackBufferOverrun,

        [Description("sov")]
        StackOverflow,

        [Description("vs")]
        VerifierStop,

        [Description("vscpp")]
        VisualCppException,

        [Description("wkd")]
        WakeDebugger,

        [Description("rto")]
        WindowsRuntimeOriginateError,

        [Description("rtt")]
        WindowsRuntimeTransformError,

        [Description("wob")]
        Wow64Breakpoint,

        [Description("wos")]
        Wow64SingleStepException,

        #endregion
    }
}
