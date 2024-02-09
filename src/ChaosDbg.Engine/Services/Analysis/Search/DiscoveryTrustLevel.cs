namespace ChaosDbg.Analysis
{
    /// <summary>
    /// Specifies to what extent we trust that the instructions discovered by a
    /// <see cref="InstructionDiscoverySource"/> are to be trusted as pointing
    /// to the start of real instructions (and not halfway in between a real instruction)
    /// </summary>
    public enum DiscoveryTrustLevel
    {
        Untrusted,
        SemiTrusted,
        Trusted
    }
}
