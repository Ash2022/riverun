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
        log.AppendLine("Start: " + startPart.partId + "  End: " + endPart.partId);

        // Collect start & goal nodes
        List<GraphModel.GraphNode> startNodes = _graph.PartToNodes[startPart.partId];
        HashSet<GraphModel.GraphNode> goalNodes = new HashSet<GraphModel.GraphNode>(_graph.PartToNodes[endPart.partId]);

        // Dijkstra containers
        Dictionary<GraphModel.GraphNode, float> dist = new Dictionary<GraphModel.GraphNode, float>();
        Dictionary<GraphModel.GraphNode, GraphModel.GraphEdge> prev = new Dictionary<GraphModel.GraphNode, GraphModel.GraphEdge>();
        HashSet<GraphModel.GraphNode> visited = new HashSet<GraphModel.GraphNode>();

        // "Open set" stored as a simple list
        List<GraphModel.GraphNode> open = new List<GraphModel.GraphNode>();

        // init
        for (int i = 0; i < startNodes.Count; i++)
        {
            dist[startNodes[i]] = 0f;
            open.Add(startNodes[i]);
        }

        GraphModel.GraphNode reachedGoal = null;

        while (open.Count > 0)
        {
            // find node with smallest dist
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
                reachedGoal = u;
                log.AppendLine("Reached goal node: " + u.Id.ToString() + " cost=" + best);
                break;
            }

            // relax edges
            for (int i = 0; i < u.Edges.Count; i++)
            {
                GraphModel.GraphEdge e = u.Edges[i];
                GraphModel.GraphNode v = e.To;
                if (visited.Contains(v)) continue;

                // ---- NEW RULE: no two internal edges in a row within same part ----
                bool thisIsInternal = (e.From.Part == e.To.Part);

                GraphModel.GraphEdge prevE;
                bool hadPrev = prev.TryGetValue(u, out prevE);
                if (hadPrev)
                {
                    bool prevWasInternal = (prevE.From.Part == prevE.To.Part);
                    if (prevWasInternal && thisIsInternal && prevE.From.Part == e.From.Part)
                        continue; // skip this edge
                }
                // -------------------------------------------------------------------

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

        if (reachedGoal == null)
        {
            log.AppendLine("No path found.");
            Debug.Log(log.ToString());
            return new PathModel(); // default = failed
        }

        // reconstruct nodes
        List<GraphModel.GraphNode> nodePath = new List<GraphModel.GraphNode>();
        GraphModel.GraphNode cur = reachedGoal;
        while (prev.ContainsKey(cur))
        {
            nodePath.Add(cur);
            cur = prev[cur].From;
        }
        nodePath.Add(cur);
        nodePath.Reverse();

        // collapse to parts AND build Traversals (first/last special)
        List<PlacedPartInstance> partPath = new List<PlacedPartInstance>();
        List<PathModel.PartTraversal> travs = new List<PathModel.PartTraversal>();

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

            PathModel.PartTraversal t;
            t.partId = part.partId;
            t.entryExit = entryExit;
            t.exitExit = exitExit;
            travs.Add(t);

            if (partPath.Count == 0 || partPath[partPath.Count - 1] != part)
                partPath.Add(part);

            a = b;
        }

        // First / last adjustments
        if (travs.Count > 0)
        {
            // first
            var first = travs[0];
            first.entryExit = -1;
            travs[0] = first;

            // last (if different block)
            var lastIdx = travs.Count - 1;
            var last = travs[lastIdx];
            last.exitExit = -1;
            travs[lastIdx] = last;
        }

        float total = dist[reachedGoal];

        // logs
        log.AppendLine("NodePath:");
        for (int i = 0; i < nodePath.Count; i++)
            log.AppendLine("  " + nodePath[i].Id + " (part " + nodePath[i].Part.partId + ")");

        log.AppendLine("PartPath:");
        for (int i = 0; i < partPath.Count; i++)
            log.AppendLine("  " + partPath[i].partId);

        log.AppendLine("TotalCost: " + total);
        Debug.Log(log.ToString());

        PathModel result = new PathModel();
        result.Success = true;
        result.Traversals = travs;
        result.TotalCost = total;
        return result;
    }
}
