using System;
using System.Collections.Generic;
using System.Linq;

namespace ChaosDbg.Analysis
{
    //Based on SequenceSearchState.java from Ghidra, licensed under the Apache License.
    //See ThirdPartyNotices.txt for full license notice.

    /// <summary>
    /// Provides facilities for simultaneously comparing a byte value against multiple possible <see cref="ByteSequence"/> values
    /// by constructing a tree that can be traversed to "dig down" to possible patterns that may match. e.g. given the patterns
    /// 0xff00 and 0xff01, this will result in four tree nodes: Root -> 0xff, 0xff -> 0x00 and 0xff -> 0x01.
    /// </summary>
    class ByteSequenceTreeNode
    {
        private const int LevelWidth = 256;

        /// <summary>
        /// Gets all sequences that could still match at this level of the tree,
        /// or on any of its sub nodes.
        /// </summary>
        public List<ByteSequence> Candidates { get; } = new List<ByteSequence>();

        /// <summary>
        /// Gets all sequences that have been completely matched against
        /// at this level of the tree.
        /// </summary>
        public List<ByteSequence> Complete { get; } = new List<ByteSequence>();

        public ByteSequenceTreeNode[] ChildNodes { get; } = new ByteSequenceTreeNode[LevelWidth];

        public int Depth { get; }

        public ByteSequenceTreeNode Parent { get; }

        public byte? MatchedByte { get; }

        private ByteSequenceTreeNode(int depth, ByteSequenceTreeNode parent, byte? matchedByte)
        {
            Depth = depth;
            Parent = parent;
            MatchedByte = matchedByte;
        }

        public ByteSequenceTreeNode this[byte index] => ChildNodes[index];

        public static ByteSequenceTreeNode BuildTree(ByteSequence[] patterns)
        {
            var root = new ByteSequenceTreeNode(0, null, null);

            foreach (var pattern in patterns)
                root.Add(pattern);

            BuildLevels(root);

            return root;
        }

        private void Add(ByteSequence sequence)
        {
            Candidates.Add(sequence);

            if (sequence.Length == Depth)
                Complete.Add(sequence);
        }

        private static void BuildLevels(ByteSequenceTreeNode root)
        {
            var byteDepth = 0;

            var current = new[] { root };

            List<ByteSequenceTreeNode> nextLevels = new List<ByteSequenceTreeNode>();

            do
            {
                foreach (var item in current)
                {
                    for (var i = 0; i < LevelWidth; i++)
                    {
                        ByteSequenceTreeNode nextLevel = null;

                        foreach (var candidate in item.Candidates)
                        {
                            if (candidate.HasByte(byteDepth, i))
                            {
                                //We need to be considered on this level!

                                if (nextLevel == null)
                                    nextLevel = new ByteSequenceTreeNode(byteDepth + 1, item, (byte) i);

                                nextLevel.Add(candidate);
                            }
                        }

                        item.ChildNodes[i] = nextLevel;

                        if (nextLevel != null)
                            nextLevels.Add(nextLevel);
                    }
                }

                //When a sequence contains a pattern that matches any value in a certain position,
                //this can result in us getting a bunch of duplicates, since a sequence will respond
                //"oh yes I match that" against every single value that's shown to it at a given
                //byte position
                var dict = new Dictionary<ByteSequenceTreeNode, ByteSequenceTreeNode>(ByteSequenceTreeNodeEqualityComparer.Instance);

                var toRemove = new List<ByteSequenceTreeNode>();

                foreach (var item in nextLevels)
                {
                    if (dict.TryGetValue(item, out var existing))
                    {
                        //Ghidra normally uses a function called "merge" to merge items together when removing them. Until we've shown that we need that,
                        //we aren't going to worry about such complexity.
                        if (item.Complete.Count > 0 && !existing.Complete.All(v => item.Complete.Any(c => c == v)))
                            throw new NotImplementedException("Don't know how to remove a node whose completed items were different from its apparent duplicate.");

                        toRemove.Add(item);
                        item.Parent.ChildNodes[item.MatchedByte.Value] = existing;
                    }
                    else
                        dict[item] = item;
                }

                current = nextLevels.Except(toRemove).ToArray();
                nextLevels.Clear();

                byteDepth++;
            } while (current.Length > 0);
        }

        public IEnumerable<ByteMatch> GetMatches(byte[] bytes)
        {
            ByteSequenceTreeNode current;

            for (var i = 0; i < bytes.Length; i++)
            {
                //Start at the root
                current = this;
                var j = i;

                do
                {
                    if (current.Complete.Count > 0)
                    {
                        foreach (var item in current.Complete)
                            yield return new ByteMatch(i, item);
                    }

                    //We traversed down so many nodes
                    //we've run out of bytes
                    if (j >= bytes.Length)
                        break;

                    var currentByte = bytes[j];

                    current = current.ChildNodes[currentByte];
                    j++;
                } while (current != null);
            }
        }

        public override string ToString()
        {
            if (Depth > 0)
            {
                var bytes = new List<byte>();

                bytes.Add(MatchedByte.Value);

                var parent = Parent;

                while (parent != null && parent.MatchedByte != null)
                {
                    bytes.Add(parent.MatchedByte.Value);
                    parent = parent.Parent;
                }

                bytes.Reverse();

                return "0x" + string.Join(string.Empty, bytes.Select(v => v.ToString("X2")));
            }
            else
                return "Root";
        }
    }
}
