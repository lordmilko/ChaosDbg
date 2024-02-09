using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Analysis;
using ChaosDbg.Graph;

namespace ChaosDbg.Tests
{
    class MockChunkVertex : IChunkVertex
    {
        public long Addr { get; set; }
        
        public HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsFromThis { get; } = new(ChunkVertex.RefEqualityComparer.Instance);

        public HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsToThis { get; } = new(ChunkVertex.RefEqualityComparer.Instance);

        public IGraphVertex Original { get; }
        
        internal MockChunkVertex(IGraphVertex original)
        {
            Original = original;
        }
        
        private IGraphEdge[] edges;

        //Note if you start adding items to ExternalRefsFromThis
        //after accessing this, this will become invalid
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IGraphEdge[] Edges => edges ??= ExternalRefsFromThis.Select(v => (IGraphEdge) new Edge(this, v.Other)).ToArray();

        public void ClearEdges()
        {
            edges = null;
        }

        class Edge : IGraphEdge
        {
            public IGraphVertex Source { get; }
            public IGraphVertex Target { get; }

            public Edge(IGraphVertex source, IGraphVertex target)
            {
                Source = source;
                Target = target;
            }
        }

        public override string ToString()
        {
            return Original.ToString();
        }
    }
}
