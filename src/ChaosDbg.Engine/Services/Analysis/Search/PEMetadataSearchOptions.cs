using System;

namespace ChaosDbg.Analysis
{
    [Flags]
    public enum PEMetadataSearchOptions
    {
        None = 0,

        Exports = 1,
        UnwindData = 2,
        GuardCFFunctionTable = 4,
        GuardEHContinuationTable = 8,
        Config = GuardCFFunctionTable | GuardEHContinuationTable,

        /// <summary>
        /// Specifies that embedded PE metadata (exports, x64 unwind information) should be used to identify
        /// function locations (or locations within functions).
        /// </summary>
        Embedded = Exports | UnwindData | Config,

        Call = 16,

        /// <summary>
        /// Specifies that symbols should be used to identify metadata items within a PE file.
        /// </summary>
        Symbols = 32,

        /// <summary>
        /// Specifies that byte patterns should be used to identify function starts.
        /// </summary>
        Patterns = 64,

        /// <summary>
        /// Specifies that all patterns should be used to try and identify functions.
        /// </summary>
        All = Embedded | Symbols | Patterns | Call
    }
}
