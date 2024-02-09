using System.Collections.Generic;
using System.Diagnostics;

namespace ChaosDbg.Analysis
{
    /// <summary>
    /// Represents a collection of interrelated chunks that may be part of one
    /// or more functions.
    /// </summary>
    [DebuggerDisplay("Count = {Vertices.Count}")]
    class ChunkGraph
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public List<IChunkVertex> Vertices { get; }

        internal ChunkGraph(List<IChunkVertex> vertices)
        {
            Vertices = vertices;
        }
    }
}
