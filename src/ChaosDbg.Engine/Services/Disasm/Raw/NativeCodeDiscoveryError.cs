namespace ChaosDbg.Disasm
{
    public enum NativeCodeDiscoveryError
    {
        None,
        InvalidInstruction,
        UnknownInterrupt,
        FunctionSizeThresholdReached,
        EmptyChunk
    }
}
