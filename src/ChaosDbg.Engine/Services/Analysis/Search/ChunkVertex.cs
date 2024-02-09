using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Graph;

namespace ChaosDbg.Analysis
{
    interface IChunkVertex : IGraphVertex
    {
        long Addr { get; }

        HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsFromThis { get; }

        HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsToThis { get; }

        void ClearEdges();
    }

    class ChunkVertex : IChunkVertex
    {
        internal class RefEqualityComparer : IEqualityComparer<(IChunkVertex Other, NativeXRefKind Kind)>
        {
            public static readonly RefEqualityComparer Instance = new RefEqualityComparer();


            public bool Equals((IChunkVertex Other, NativeXRefKind Kind) x, (IChunkVertex Other, NativeXRefKind Kind) y)
            {
                return x.Other.Equals(y.Other);
            }

            public int GetHashCode((IChunkVertex Other, NativeXRefKind Kind) obj) =>
                obj.Other.GetHashCode();
        }

        public long Addr => Builder.StartAddress;

        public NativeFunctionChunkBuilder Builder { get; }

        public HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsFromThis { get; } = new(RefEqualityComparer.Instance);

        public HashSet<(IChunkVertex Other, NativeXRefKind Kind)> ExternalRefsToThis { get; } = new(RefEqualityComparer.Instance);

        public ChunkVertex(NativeFunctionChunkBuilder builder)
        {
            Builder = builder;
        }

        public void AddRefFromThis(ChunkVertex other, NativeXRefKind kind)
        {
            ExternalRefsFromThis.Add((other, kind));
            other.ExternalRefsToThis.Add((this, kind));
        }

        public override string ToString()
        {
            return Builder.ToString();
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
    }
}
