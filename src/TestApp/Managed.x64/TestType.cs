namespace TestApp
{
    public enum TestType
    {
        CordbEngine_Thread_StackTrace_ManagedFrames,
        CordbEngine_Thread_StackTrace_InternalFrames,
        CordbEngine_Thread_TLS,
        CordbEngine_Thread_TLS_Extended,
        CordbEngine_Thread_Type,

        DbgEngEngine_ChildProcess
    }

    public enum NativeTestType
    {
        Com = 1
    }
}
