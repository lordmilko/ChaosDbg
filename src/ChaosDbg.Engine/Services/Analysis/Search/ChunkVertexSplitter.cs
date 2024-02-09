using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ChaosDbg.Graph;

namespace ChaosDbg.Analysis
{
    class ChunkVertexSplitter
    {
        Dictionary<IChunkVertex, List<IChunkVertex>> rootToClaimedMap;
        Dictionary<IChunkVertex, List<IChunkVertex>> vertexToClaimedMap;

        HashSet<IChunkVertex> seen;
        Queue<IChunkVertex> queue;

        HashSet<List<IChunkVertex>> deferredListsToKeep;
        HashSet<List<IChunkVertex>> deferredListsToIgnore;

        private void Reset()
        {
            rootToClaimedMap = null;
            vertexToClaimedMap = null;

            seen = null;
            queue = null;
            deferredListsToKeep = null;
            deferredListsToIgnore = null;
        }

        internal ChunkGraph[] ProcessAndSplitChunkGraphs(
            ChunkGraph[] inputList,
            Func<long, bool> predicate)
        {
            List<ChunkGraph> newList = null;
            List<ChunkGraph> splitChunks = null;

            var rootVertices = new List<IChunkVertex>();

            for (var i = 0; i < inputList.Length; i++)
            {
                var graph = inputList[i];

                foreach (var vertex in graph.Vertices)
                {
                    if (predicate(vertex.Addr))
                    {
                        //This vertex is called by somewhere else
                        rootVertices.Add(vertex);
                    }
                }

                if (rootVertices.Count > 1)
                {
#if DEBUG
                    Log($"    Found {rootVertices.Count} root nodes:");

                    foreach (var root in rootVertices)
                        Log($"        \"{root}\"");
#endif

                    //More than one vertex was called upon. We have more than one function contained in this chunk

                    if (splitChunks == null)
                        splitChunks = new List<ChunkGraph>();

                    SplitChunkVertices(graph, rootVertices, splitChunks);

                    if (newList == null)
                    {
                        //Seed the new list with all of the graphs we skipped over so far
                        newList = new List<ChunkGraph>();

                        for (var k = 0; k < i; k++)
                            newList.Add(inputList[k]);
                    }

                    foreach (var item in splitChunks)
                        newList.Add(item);

                    splitChunks.Clear();
                }
                else
                {
                    if (newList != null)
                        newList.Add(graph);
                }

                rootVertices.Clear();
            }

#if DEBUG
            if (newList != null)
            {
                var missing = inputList.SelectMany(v => v.Vertices).Except(newList.SelectMany(v => v.Vertices)).ToArray();

                Debug.Assert(missing.Length == 0, "Lost some nodes!");

                //Check for duplicates
                var duplicates = newList.SelectMany(v => v.Vertices).GroupBy(v => v).Where(v => v.Count() > 1).Select(v => v.Key).ToArray();

                var culprits = newList.Where(v => v.Vertices.Any(a => duplicates.Contains(a))).ToArray();

                Debug.Assert(duplicates.Length == 0, "Got duplicate nodes!");
            }
#endif

            return newList?.ToArray() ?? inputList;
        }

        private void SplitChunkVertices(ChunkGraph graph, IList<IChunkVertex> rootVertices, List<ChunkGraph> splitChunks)
        {
            //Fast path: each vertex is supposed to be its own function
            if (rootVertices.Count == graph.Vertices.Count)
            {
                foreach (var vertex in graph.Vertices)
                {
                    var newVertices = new List<IChunkVertex> { vertex };
                    CleanVertexReferences(newVertices);
                    splitChunks.Add(new ChunkGraph(newVertices));
                }

                return;
            }

            Reset();

            //Create a DAG out of the vertices in the graph, starting from the root nodes so that they are at the top of any loops that may occur
            var ordered = graph.Vertices.OrderBy(v => rootVertices.Contains(v) == false).Cast<IGraphVertex>().ToArray();
            var (dag, _) = DirectedAcyclicGraph.Create<SimpleDagVertex<IChunkVertex>>(ordered, (o, e) => new SimpleDagVertex<IChunkVertex>((IChunkVertex) o, e));
            Debug.Assert(dag.Length == graph.Vertices.Count, "Input graph had stray references that should have been removed");

            Array.Reverse(dag);

            //Maps each root vertex to the list of chunks it has claimed ownership of
            rootToClaimedMap = new Dictionary<IChunkVertex, List<IChunkVertex>>();

            //Maps each vertex to the list of chunks it has been claimed as a part of. In the case of root
            //vertices, this list is the same list found in rootToClaimedMap
            vertexToClaimedMap = graph.Vertices.ToDictionary(v => v, v => new List<IChunkVertex>());

            foreach (var root in rootVertices)
                rootToClaimedMap[root] = vertexToClaimedMap[root];

            var claimedSet = new HashSet<IChunkVertex>();

            //Analyze each of our children to figure out whether we should own them, or whether we have a sibling adjacent in the tree that should own them instead
            foreach (var parent in dag)
            {
                var isParentRoot = rootToClaimedMap.ContainsKey(parent.Original);
                var refsFromThis = parent.EdgesFromThis;

                Log($"    Processing parent \"{parent}\" (Root: {isParentRoot})");

                foreach (var child in refsFromThis)
                {
                    Log($"        Processing child \"{child}\" (Root: {rootToClaimedMap.ContainsKey(child.Target.Original)})");

                    if (claimedSet.Contains(child.Target.Original))
                    {
                        Log("            Node has already been claimed. Continuing");
                        continue;
                    }

                    var isChildRoot = rootToClaimedMap.ContainsKey(child.Target.Original);

                    //If a parent and child are both vertexes, they need to become separate
                    if (isParentRoot && isChildRoot)
                    {
                        Log("            Both parent and child are roots. Continuing");
                        continue;
                    }

                    //Root nodes don't have parents. We don't want other nodes claiming us; WE do the claiming
                    if (isChildRoot)
                        continue;

                    if (child.Target.EdgesToThis.Count == 1)
                    {
                        Log($"            \"{parent}\" is the only parent of \"{child}\". Parent \"{parent}\" claims child \"{child}\"");

                        //Only a single parent claims this child. That parent is us
                        Debug.Assert(claimedSet.Add(child.Target.Original));
                        vertexToClaimedMap[parent.Original].Add(child.Target.Original);
                    }
                    else
                    {
                        /* Our child has multiple parents pointing to it. We know that the child isn't a root, so it's fair game who claims it
                         * This list contains both the value of our parent variable as well as the other parents we're competing with. i.e. suppose
                         * "parent" is A and "child" is B
                         *
                         *  A
                         *   \
                         *    B
                         *
                         * In the current iteration of the loop, A is thinking about claiming B. But then along comes C, who also holds a reference to B
                         *
                         *  A   C
                         *   \ /
                         *    B
                         *
                         * A, our current parent, is now in battle with C to claim ownership of B */

                        //While we know that _both_ parent and child are not roots, is child potentially a root itself?

#if DEBUG
                        Log($"            Have {child.Target.EdgesToThis.Count} parents to choose from:");

                        foreach (var item in child.Target.EdgesToThis)
                            Log($"                \"{item}\"");
#endif

                        //The child isn't a root. Get the closest parent referencing it
                        var candidateParents = child.Target.EdgesToThis.ToList();
                        candidateParents.Sort((a, b) => a.Target.Original.Addr.CompareTo(b.Target.Original.Addr));

                        //Get closest parent that occurs before us
                        var closest = candidateParents.FindLast(c => c.Target.Original.Addr < child.Target.Original.Addr);

                        if (closest == null)
                        {
                            //Get the closest parent that occurs after us
                            closest = candidateParents.Find(c => c.Target.Original.Addr > child.Target.Original.Addr);

                            Log($"            \"{closest}\" is the closest parent AFTER us (no candidates were BEFORE). Parent \"{closest}\" claims child \"{child}\"");
                        }
                        else
                        {
                            Log($"            \"{closest}\" is the closest parent BEFORE us. Parent \"{closest}\" claims child \"{child}\"");
                        }

                        Debug.Assert(claimedSet.Add(child.Target.Original));
                        vertexToClaimedMap[closest.Target.Original].Add(child.Target.Original);
                    }
                }
            }

            //Loop over each root and collect the nodes that they, and their descendants have claimed

            seen = new HashSet<IChunkVertex>();
            queue = new Queue<IChunkVertex>();

            deferredListsToKeep = new HashSet<List<IChunkVertex>>();
            deferredListsToIgnore = new HashSet<List<IChunkVertex>>();

            BuildNewGraphs(
                rootToClaimedMap.Keys.ToArray(),
                rootToClaimedMap,
                i => !rootToClaimedMap.ContainsKey(i),
                splitChunks,
                true
            );

            //If two trees referenced the same node, we need to merge all those trees back together? or do we need to tie break?
            var unreferencedNonRoots = dag.Where(v => !seen.Contains(v.Original) && !rootToClaimedMap.ContainsKey(v.Original) && v.EdgesToThis.Count == 0).Select(v => v.Original).ToHashSet();

            var unreferencedRootNodeKeys = vertexToClaimedMap.Keys.Where(v => unreferencedNonRoots.Contains(v)).ToArray();

            BuildNewGraphs(
                unreferencedRootNodeKeys,
                vertexToClaimedMap,
                i => true,
                splitChunks,
                false
            );

            if (deferredListsToKeep.Count > 0)
            {
                var diffLists = deferredListsToKeep.Except(deferredListsToIgnore).ToArray();

                Debug.Assert(diffLists.Length > 0);

                foreach (var list in diffLists)
                {
                    CleanVertexReferences(list);

                    splitChunks.Add(new ChunkGraph(list));
                }
            }
        }

        void BuildNewGraphs(
                IChunkVertex[] roots,
                Dictionary<IChunkVertex, List<IChunkVertex>> dict,
                Func<IChunkVertex, bool> isValid,
                List<ChunkGraph> splitChunks,
                bool isRootPass)
        {
            foreach (var root in roots)
            {
                var rootValue = dict[root];
                var needOverwrite = deferredListsToKeep.Contains(rootValue);

                foreach (var item in rootValue)
                {
                    //Roots shouldn't contain references to other roots
                    Debug.Assert(isValid(item));
                    queue.Enqueue(item);
                    seen.Add(item);
                }

                var deferAdd = false;

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();

                    if (vertexToClaimedMap.TryGetValue(current, out var currentList))
                    {
                        //ntdll!LdrUnloadAlternateResourceModuleEx$fin$0 already merged itself with ntdll!LdrUnloadAlternateResourceModuleEx's list.
                        //ntdll!LdrUnloadAlternateResourceModule is now attempting to process LdrUnloadAlternateResourceModuleEx. Merge Module and ModuleEx's
                        //lists together, and replace Module's list with the super list we've already been building with previously
                        var addToRoot = !deferredListsToKeep.Contains(currentList);

                        foreach (var item in currentList)
                        {
                            //If we have roots A and C, and have the relationship
                            //A -> B -> C, we need to catch that and break the relationship
                            //to C
                            if (isValid(item) && seen.Add(item))
                            {
                                if (addToRoot)
                                    rootValue.Add(item);

                                queue.Enqueue(item);
                            }
                        }

                        if (!addToRoot || needOverwrite)
                        {
                            if (needOverwrite)
                            {
                                //I don't know that we even need to do this; ostensibly we'll write all child nodes to the rootValue list.
                                //But for extra protection, in case theres 0 refs to this on any nodes (which I guess doesn't make sense for descendants)
                                //we'll just replace their list with that of the rootValue list
                                dict[current] = rootValue;
                            }
                            else
                            {
                                needOverwrite = true;

                                foreach (var item in rootValue)
                                {
                                    //No need to queue anything since we're just completely replacing rootValue with currentList which contains everything we would gain from queuing anyway
                                    if (!currentList.Contains(item))
                                        currentList.Add(item);
                                }

                                dict[root] = currentList;
                                rootValue = currentList;
                            }

                            deferAdd = true;
                        }
                    }
                }

                //Now collect all of the lists that our original parents were apart of

                if (!rootValue.Contains(root))
                    rootValue.Insert(0, root);

                if (!isRootPass)
                {
                    var parentRoots = 0;

                    foreach (var refToThis in root.ExternalRefsToThis)
                    {
                        if (rootToClaimedMap.ContainsKey(refToThis.Other))
                            parentRoots++;

                        if (vertexToClaimedMap.TryGetValue(refToThis.Other, out var parentList))
                        {
                            //Add all our items in if the parent doesn't have them yet

                            if (parentList != rootValue)
                                deferredListsToIgnore.Add(parentList);

                            deferredListsToKeep.Add(rootValue);

                            foreach (var item in parentList)
                            {
                                if (!rootValue.Contains(item))
                                    rootValue.Add(item);
                            }

                            if (!rootValue.Contains(refToThis.Other))
                                rootValue.Add(refToThis.Other);

                            deferAdd = true;
                            needOverwrite = true;

                            //Replace the parent's list with our list
                            vertexToClaimedMap[refToThis.Other] = rootValue;
                        }
                    }

                    if (parentRoots > 0)
                    {
                        //I think this means that rootToClaimedMap.ContainsKey(refToThis.Other), but it may also be true that we added this chuld to our rootValue list

                        throw new NotImplementedException("Don't know how to tiebreak between our pseudo-root and a real root that has claimed a child node");
                    }
                }

                if (isRootPass || !deferAdd)
                {
                    CleanVertexReferences(rootValue);

                    splitChunks.Add(new ChunkGraph(rootValue));
                }
            }
        }

        private static void CleanVertexReferences(List<IChunkVertex> newVertices)
        {
            //We've split the vertices from our original graph, but we need to
            //remove all the references to the chunks that will no longer exist
            //in our new graph

            var newVerticesSet = newVertices.ToHashSet();
            var toRemove = new HashSet<IChunkVertex>();

            foreach (var newVertex in newVertices)
            {
                foreach (var refFromThis in newVertex.ExternalRefsFromThis)
                {
                    if (!newVerticesSet.Contains(refFromThis.Other))
                        toRemove.Add(refFromThis.Other);
                }

                newVertex.ExternalRefsFromThis.RemoveWhere(v => toRemove.Contains(v.Other));
                toRemove.Clear();

                foreach (var refToThis in newVertex.ExternalRefsToThis)
                {
                    if (!newVerticesSet.Contains(refToThis.Other))
                        toRemove.Add(refToThis.Other);
                }

                newVertex.ExternalRefsToThis.RemoveWhere(v => toRemove.Contains(v.Other));
                toRemove.Clear();
                newVertex.ClearEdges();
            }
        }

        [Conditional("DEBUG")]
        internal void Log(string message)
        {
            //Debug.WriteLine(message);
        }
    }
}
