using System.Collections.Generic;
using UnityEngine;

public class PathModel
{
    public bool Success;
    public List<PartTraversal> Traversals;
    public float TotalCost;

    public PathModel()
    {
        Success = false;
        Traversals = new List<PartTraversal>();
        TotalCost = 0f;
    }

    public struct PartTraversal
    {
        public string partId;
        public int entryExit; // where we entered this part
        public int exitExit;  // where we left this part
    }
}



public class PathFinder
{
    private GraphModel _graph;

    public PathFinder() { }

    public void Init(GraphModel graph)
    {
        _graph = graph;
    }

    public PathModel GetPath(PlacedPartInstance startPart, PlacedPartInstance endPart)
    {
        var log = new System.Text.StringBuilder();
        log.AppendLine("=== PathFinder ===");
        log.AppendLine($"Start: {startPart.partId}  End: {endPart.partId}");

        // start / goal sets
        var startNodes = _graph.PartToNodes[startPart.partId];
        var goalNodes = new HashSet<GraphModel.GraphNode>(_graph.PartToNodes[endPart.partId]);

        // Dijkstra state
        var dist = new Dictionary<GraphModel.GraphNode, float>();
        var prev = new Dictionary<GraphModel.GraphNode, GraphModel.GraphEdge>();
        var visited = new HashSet<GraphModel.GraphNode>();
        var open = new List<GraphModel.GraphNode>();

        for (int i = 0; i < startNodes.Count; i++)
        {
            dist[startNodes[i]] = 0f;
            open.Add(startNodes[i]);
        }

        // store ALL goal hits
        var found = new List<(GraphModel.GraphNode goal, float cost)>();

        while (open.Count > 0)
        {
            // extract-min
            GraphModel.GraphNode u = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                float d;
                if (dist.TryGetValue(open[i], out d) && d < best)
                {
                    best = d;
                    u = open[i];
                }
            }
            open.Remove(u);
            visited.Add(u);

            if (goalNodes.Contains(u))
            {
                found.Add((u, best));
                log.AppendLine($"Reached goal node: {u.Id} cost={best}");
                // DO NOT break; continue to discover other equal/longer paths
            }

            // relax
            for (int i = 0; i < u.Edges.Count; i++)
            {
                var e = u.Edges[i];
                var v = e.To;
                if (visited.Contains(v)) continue;

                // rule: no 2 internal edges in row within same part
                bool thisInternal = (e.From.Part == e.To.Part);
                GraphModel.GraphEdge prevE;
                if (prev.TryGetValue(u, out prevE))
                {
                    bool prevInternal = (prevE.From.Part == prevE.To.Part);
                    if (prevInternal && thisInternal && prevE.From.Part == e.From.Part) continue;
                }

                float nd = best + e.Cost;
                float old;
                if (!dist.TryGetValue(v, out old) || nd < old)
                {
                    dist[v] = nd;
                    prev[v] = e;
                    if (!open.Contains(v)) open.Add(v);
                }
            }
        }

        if (found.Count == 0)
        {
            log.AppendLine("No path found.");
            Debug.Log(log.ToString());
            return new PathModel(); // failed
        }

        // sort by cost
        found.Sort((a, b) => a.cost.CompareTo(b.cost));

        // dump all candidates
        log.AppendLine("--- Candidate paths ---");
        for (int k = 0; k < found.Count; k++)
        {
            var np = ReconstructNodePath(found[k].goal, prev);
            float c = found[k].cost;
            log.AppendLine($"#{k} cost={c} nodes={np.Count}");
            DumpEdges(np, log);
        }

        // take best
        var bestGoal = found[0].goal;
        float total = found[0].cost;
        var nodePathBest = ReconstructNodePath(bestGoal, prev);

        // build Traversals + PathModel (same as before)
        var travs = BuildTraversals(nodePathBest);
        float dummy; // not needed

        // logs
        log.AppendLine("=== Chosen path ===");
        DumpEdges(nodePathBest, log);
        log.AppendLine("TotalCost: " + total);
        Debug.Log(log.ToString());

        return new PathModel
        {
            Success = true,
            Traversals = travs,
            TotalCost = total
        };
    }

    /* ---------- helpers ---------- */

    private List<GraphModel.GraphNode> ReconstructNodePath(GraphModel.GraphNode goal,
                                                           Dictionary<GraphModel.GraphNode, GraphModel.GraphEdge> prev)
    {
        var nodes = new List<GraphModel.GraphNode>();
        var cur = goal;
        while (prev.ContainsKey(cur))
        {
            nodes.Add(cur);
            cur = prev[cur].From;
        }
        nodes.Add(cur);
        nodes.Reverse();
        return nodes;
    }

    private void DumpEdges(List<GraphModel.GraphNode> nodePath, System.Text.StringBuilder sb)
    {
        float accum = 0f;
        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            var from = nodePath[i];
            var to = nodePath[i + 1];
            // find the edge
            var edge = from.Edges.Find(ed => ed.To == to);
            float cost = edge != null ? edge.Cost : 0f;
            accum += cost;
            sb.AppendLine($"  {from.Part.partId}:{from.Exit.exitIndex} -> {to.Part.partId}:{to.Exit.exitIndex}  cost={cost}");
        }
        sb.AppendLine($"  (sum={accum})");
    }

    private List<PathModel.PartTraversal> BuildTraversals(List<GraphModel.GraphNode> nodePath)
    {
        var travs = new List<PathModel.PartTraversal>();
        int a = 0;
        while (a < nodePath.Count)
        {
            var startNode = nodePath[a];
            var part = startNode.Part;
            int entryExit = startNode.Exit.exitIndex;
            int exitExit = entryExit;

            int b = a + 1;
            while (b < nodePath.Count && nodePath[b].Part == part)
            {
                exitExit = nodePath[b].Exit.exitIndex;
                b++;
            }

            travs.Add(new PathModel.PartTraversal
            {
                partId = part.partId,
                entryExit = entryExit,
                exitExit = exitExit
            });

            a = b;
        }

        if (travs.Count > 0)
        {
            var first = travs[0]; first.entryExit = -1; travs[0] = first;
            var lastI = travs.Count - 1;
            var last = travs[lastI]; last.exitExit = -1; travs[lastI] = last;
        }
        return travs;
    }

}
