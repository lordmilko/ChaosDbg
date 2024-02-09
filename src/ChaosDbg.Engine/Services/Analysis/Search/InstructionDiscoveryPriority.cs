using System;
using System.Diagnostics;

namespace ChaosDbg.Analysis
{
    /// <summary>
    /// Describes the priority of an <see cref="InstructionDiscoverySource"/> for the purposes of
    /// arranging items within a <see cref="PriorityQueue{TElement, TPriority}"/>.
    /// </summary>
    [DebuggerDisplay("Evidence = {bits}, Weight = {(int) foundBy}")]
    struct InstructionDiscoveryPriority : IComparable<InstructionDiscoveryPriority>
    {
        private FoundBy foundBy;
        private int bits;

        public InstructionDiscoveryPriority(FoundBy foundBy)
        {
            /* The priority of a candidate is determined by two heuristics:
             * 1. How many unique sources have identified this candidate
             * 2. What is the total weight of those sources
             *
             * e.g. suppose we have two candidates, with Xfg+Export and Symbol+UnwindData respectively.
             * Both candidates have two flags set, however Symbol+UnwindData is worth more than Xfg+Pattern,
             * so has a higher priority.
             *
             * By contrast, if we instead had Xfg+Export vs UnwindData, despite the fact UnwindData has
             * a greater weight, Xfg+Export has two pieces of evidence going for it, so has higher priority. */

            this.foundBy = foundBy;

            var num = (int) foundBy;

            bits = 0;

            while (num > 0)
            {
                var masked = num & 0x1;

                if (masked == 1)
                    bits++;

                num >>= 1;
            }
        }

        public int CompareTo(InstructionDiscoveryPriority other)
        {
            //The more sources signal a function is valid, the better

            var bitCompare = bits.CompareTo(other.bits);

            if (bitCompare != 0)
                return bitCompare;

            //If we have the same number of sources signalling a function is valid, instead compare
            //the "weights" of those sources (e.g. UnwindData+Symbol is stronger than Pattern+Call)
            var priorityCompare = foundBy.CompareTo(other.foundBy);

            return priorityCompare;
        }
    }
}
