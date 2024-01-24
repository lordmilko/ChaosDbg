namespace ChaosDbg.Disasm
{
    public enum BadFunctionReason
    {
        None,
        InvalidInstruction,
        UnknownInterrupt,
        FunctionSizeThresholdReached,
        EmptyChunk
    }
}
