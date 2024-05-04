using System.Collections.Generic;

namespace ChaosDbg.Analysis
{
    class ByteSequenceTreeNodeEqualityComparer : IEqualityComparer<ByteSequenceTreeNode>
    {
        public static readonly ByteSequenceTreeNodeEqualityComparer Instance = new ByteSequenceTreeNodeEqualityComparer();

        public bool Equals(ByteSequenceTreeNode x, ByteSequenceTreeNode y)
        {
            if (x.Candidates.Count != y.Candidates.Count)
                return false;

            for (var i = 0; i < x.Candidates.Count; i++)
            {
                if (x.Candidates[i] != y.Candidates[i])
                    return false;
            }

            return true;
        }

        public int GetHashCode(ByteSequenceTreeNode obj)
        {
            var hashCode = 0;

            foreach (var item in obj.Candidates)
                hashCode ^= item.GetHashCode();

            return hashCode;
        }
    }
}
