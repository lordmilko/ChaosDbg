namespace ChaosDbg.Analysis
{
    public enum NativeXRefKind
    {
        ConditionalBranch,
        UnconditionalBranch,
        IndirectBranch,
        JumpTable,
        UnwindInfo,

        /// <summary>
        /// Specifies that the value is the target of a call instruction.
        /// </summary>
        Call,

        /// <summary>
        /// Specifies that an RVA was used in a data structure or operand that points to another location within the assembly.
        /// </summary>
        RVA
    }
}
