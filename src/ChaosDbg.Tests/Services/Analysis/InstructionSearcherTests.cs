using System;
using System.Collections.Generic;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosDbg.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static ChaosDbg.Tests.GraphTests;
using static ChaosDbg.Tests.InstructionSearcherTests.Direction;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class InstructionSearcherTests
    {
        #region SimpleBranchWithMerge

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithMerge_Root_A()
        {
            Test(
                SimpleBranchWithMerge,
                new[] { "A" },
                r => r.Verify(
                    g => g.Verify("A", "B", "C")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithMerge_Root_AB()
        {
            Test(
                SimpleBranchWithMerge,
                new[] { "A", "B" },
                r => r.Verify(
                    g => g.Verify("A"),
                    g => g.Verify("B", "C")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithMerge_Root_ABC()
        {
            Test(
                SimpleBranchWithMerge,
                new[] { "A", "B", "C" },
                r => r.Verify(
                    g => g.Verify("A"),
                    g => g.Verify("B"),
                    g => g.Verify("C")
                )
            );
        }

        #endregion
        #region SimpleBranchWithTwoSkippedNodes

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithTwoSkippedNodes_Root_A()
        {
            Test(
                SimpleBranchWithTwoSkippedNodes,
                new[] { "A" },
                r => r.Verify(
                    g => g.Verify("A", "B", "C", "D")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithTwoSkippedNodes_Root_AB()
        {
            Test(
                SimpleBranchWithTwoSkippedNodes,
                new[] { "A", "B" },
                r => r.Verify(
                    g => g.Verify("A"),
                    g => g.Verify("B", "C", "D")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_SimpleBranchWithTwoSkippedNodes_Root_AB_ACloserToD()
        {
            Test(
                SimpleBranchWithTwoSkippedNodes,
                new[] { "A", "B" },
                r => r.Verify(
                    g => g.Verify("A", "D"),
                    g => g.Verify("B", "C")
                ),
                move: ("A", Before, "D") 
            );
        }

        #endregion
        #region BranchOutAndStepOnceThenMerge

        [TestMethod]
        public void InstructionSearcher_BranchOutAndStepOnceThenMerge_Root_A()
        {
            Test(
                BranchOutAndStepOnceThenMerge,
                new[] { "A" },
                r => r.Verify(
                    g => g.Verify("A", "B", "C", "D", "E", "F")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_BranchOutAndStepOnceThenMerge_Root_AB()
        {
            Test(
                BranchOutAndStepOnceThenMerge,
                new[] { "A", "B" },
                r => r.Verify(
                    g => g.Verify("A", "C", "E", "F"),
                    g => g.Verify("B", "D")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_BranchOutAndStepOnceThenMerge_Root_AB_FCloserToD()
        {
            Test(
                BranchOutAndStepOnceThenMerge,
                new[] { "A", "B" },
                r => r.Verify( 
                    g => g.Verify("A", "C", "E"),
                    g => g.Verify("B", "D", "F")
                ),
                move: ("F", Before, "D")
            );
        }

        #endregion
        #region TreeWithMergePointOwnerOnLeft

        [TestMethod]
        public void InstructionSearcher_TreeWithMergePointOwnerOnLeft_Root_A()
        {
            Test(
                TreeWithMergePointOwnerOnLeft,
                new[] { "A" },
                r => r.Verify(
                    g => g.Verify("A", "B", "C", "D")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_TreeWithMergePointOwnerOnLeft_Root_AC()
        {
            Test(
                TreeWithMergePointOwnerOnLeft,
                new[] { "A", "C" },
                r => r.Verify(
                    g => g.Verify("A", "B"),
                    g => g.Verify("C", "D")
                )
            );
        }

        #endregion

        [TestMethod]
        public void InstructionSearcher_UnreferencedNode_CarriedForward()
        {
            //A and B are roots
            //A
            //B
            //C -> D
            //C and D should be carried forward
            var original = MockNodeBuilder.Empty
                .Add("A", "B")
                .Add("B")
                .Add("C", "D")
                .Add("D")
                .Build();

            Test(
                original,
                new[] { "A", "B" }, //There has two be at least two roots, or we won't try and split anything
                r => r.Verify(
                    g => g.Verify("A"),
                    g => g.Verify("B"),
                    g => g.Verify("C", "D")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_Issue_Duplicates_1()
        {
            //the fix here was excluding items we've already seen

            //todo: repro the assert crashing before continuing. i think it might have to do with the order in which the items are ordered?

            /* RtlpCompareProtectedPolicyEntry              EtwpGuidEntryCompare      RtlpWnfNameSubscriptionCompareByStateName     RtlpCompareActivationContextGuidSectionEntryByGuid
             *        |                                         /                                   /                                               /
             *       memcmp
             *      |      |
             * 180092C63  180092C6A
             *             |
             *           180092CC8
             */

            var original = MockNodeBuilder.Empty
                .Add("RtlpCompareActivationContextGuidSectionEntryByGuid", "memcmp")
                .Add("memcmp", "180092C63", "180092C6A")
                .Add("RtlpWnfNameSubscriptionCompareByStateName", "memcmp")
                .Add("EtwpGuidEntryCompare", "memcmp")
                .Add("180092C6A", "memcmp", "180092CC8")
                .Add("RtlpCompareProtectedPolicyEntry", "memcmp")
                .Add("180092C63")
                .Add("180092CC8")
                .Build();

            Test(
                original,
                new[] { "memcmp", "RtlpWnfNameSubscriptionCompareByStateName", "EtwpGuidEntryCompare" },
                r => r.Verify(
                    g => g.Verify("memcmp", "180092C63", "180092C6A", "180092CC8"),
                    g => g.Verify("RtlpWnfNameSubscriptionCompareByStateName"),
                    g => g.Verify("EtwpGuidEntryCompare"),
                    g => g.Verify("RtlpCompareActivationContextGuidSectionEntryByGuid"),
                    g => g.Verify("RtlpCompareProtectedPolicyEntry")
                )
            );
        }

        [TestMethod]
        public void InstructionSearcher_Issue_Duplicates_2()
        {
            //when we add a parent to the child cos there were still two potential parents of the child, if that occurs for a given parent twice
            //then two different children will be laying claim to that parent.
            /*
             A   B   C
              \ / \ /
               D   E
             */
            //suppose there are no roots. if E's default parent is B, and D's default parent is also B,
            //B should be a duplicate
            var original = MockNodeBuilder.Empty
                .Add("A", "D")
                .Add("B", "D", "E")
                .Add("C", "E")
                .Add("D")
                .Add("E")
                .Build();

            Test(
                original,
                new[] { "D", "E" },
                r => r.Verify(
                    g => g.Verify("D"),
                    g => g.Verify("E"),

                    g => g.Verify("A"),
                    g => g.Verify("B"),
                    g => g.Verify("C")
                )
            );
        }

        private void Test(
            IGraphVertex[] original,
            string[] root,
            Action<ChunkGraph[]> validate,
            params (string source, Direction to, string destination)[] move)
        {
            var chunkVertices = CreateChunkVertices(original);
            var graph = new ChunkGraph(chunkVertices);

            var rootVertices = chunkVertices.Where(v => root.Contains(v.ToString())).ToArray();

            foreach (var item in move)
            {
                var source = chunkVertices.Single(v => v.ToString() == item.source);
                var destination = chunkVertices.Single(v => v.ToString() == item.destination);

                var offset = item.to == Before ? -1 : 1;

                ((MockChunkVertex) source).Addr = destination.Addr + offset;
            }

            var addrToRootMap = rootVertices.ToDictionary(v => v.Addr, v => v);

            var splitter = new ChunkVertexSplitter();

            var results = splitter.ProcessAndSplitChunkGraphs(
                new[] {graph},
                a => addrToRootMap.ContainsKey(a)
            );

            validate(results);
        }

        private List<IChunkVertex> CreateChunkVertices(IGraphVertex[] vertices)
        {
            var chunkVertices = vertices.Select(v => (IChunkVertex) new MockChunkVertex(v)).ToList();

            var dict = chunkVertices.Cast<MockChunkVertex>().ToDictionary(v => v.Original, v => v);

            for (var i = 0; i < vertices.Length; i++)
                dict[vertices[i]].Addr = i * 100;

            foreach (var vertex in chunkVertices)
            {
                foreach (var edge in ((MockChunkVertex) vertex).Original.Edges)
                {
                    var edgeVertex = dict[edge.Target];

                    vertex.ExternalRefsFromThis.Add((edgeVertex, NativeXRefKind.UnconditionalBranch));
                    edgeVertex.ExternalRefsToThis.Add((vertex, NativeXRefKind.UnconditionalBranch));
                }
            }

            return chunkVertices;
        }

        internal enum Direction
        {
            Before,
            After
        }
    }
}
