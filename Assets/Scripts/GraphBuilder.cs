using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class GraphBuilder
{
    public GraphBuilder() { }

    public GraphModel BuildGraph(List<PlacedPartInstance> parts)
    {
        var graph = new GraphModel();

        // 1) cell → part lookup
        var cellToPart = new Dictionary<Vector2Int, PlacedPartInstance>();
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            for (int c = 0; c < p.occupyingCells.Count; c++)
                cellToPart[p.occupyingCells[c]] = p;
        }

        // 2) node per exit
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            for (int e = 0; e < p.exits.Count; e++)
            {
                var exit = p.exits[e];
                var id = new GraphModel.NodeId(p.partId, exit.exitIndex);
                var node = new GraphModel.GraphNode { Id = id, Part = p, Exit = exit };
                graph.Nodes[id] = node;

                if (!graph.PartToNodes.ContainsKey(p.partId))
                    graph.PartToNodes[p.partId] = new List<GraphModel.GraphNode>();
                graph.PartToNodes[p.partId].Add(node);
            }
        }

        // helpers
        GraphModel.GraphNode GetNode(string pid, int exitIdx)
        {
            return graph.Nodes[new GraphModel.NodeId(pid, exitIdx)];
        }

        void AddEdge(GraphModel.GraphNode from, GraphModel.GraphNode to, float cost)
        {
            from.Edges.Add(new GraphModel.GraphEdge { From = from, To = to, Cost = cost });
        }

     

        bool IsOpposite(PlacedPartInstance.ExitDetails a, PlacedPartInstance.ExitDetails b)
        {
            // both exits must point at each other’s world cell
            return a.neighborCell == b.worldCell &&
                   b.neighborCell == a.worldCell;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (p.allowedPathsGroup == null) continue;

            for (int g = 0; g < p.allowedPathsGroup.Count; g++)
            {
                var group = p.allowedPathsGroup[g];
                if (group.allowedPaths == null) continue;

                for (int ap = 0; ap < group.allowedPaths.Count; ap++)
                {
                    var path = group.allowedPaths[ap]; // has entryConnectionId, exitConnectionId, length

                    var fromNode = GetNode(p.partId, path.entryConnectionId); // enter at entry
                    var toNode = GetNode(p.partId, path.exitConnectionId);  // leave via exit

                    AddEdge(fromNode, toNode, path.length);

                    // add reverse ONLY if you have a separate AllowedPath for it
                    // (your data usually has both entries explicitly, so no need here)
                }
            }
        }

        // 4) External edges (bi-directional, cost = distance between exit world cells)
        for (int i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            for (int ei = 0; ei < p.exits.Count; ei++)
            {
                var e = p.exits[ei];

                if (!cellToPart.TryGetValue(e.neighborCell, out var neighbor)) continue;
                if (neighbor == p) continue; // skip self-part

                for (int nei = 0; nei < neighbor.exits.Count; nei++)
                {
                    var ne = neighbor.exits[nei];
                    if (IsOpposite(e, ne))
                    {
                        var a = GetNode(p.partId, e.exitIndex);
                        var b = GetNode(neighbor.partId, ne.exitIndex);

                        float extCost = Vector2Int.Distance(e.worldCell, ne.worldCell); // usually 1
                        AddEdge(a, b, extCost);
                        AddEdge(b, a, extCost);
                    }
                }
            }
        }

        // 5) De-duplicate edges per node
        DedupeEdges(graph);

        // 6) Debug dump
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== GraphBuilder DEBUG ===");
        sb.AppendLine("Parts: " + parts.Count + "   Nodes: " + graph.Nodes.Count);

        // Node summary
        foreach (var kvp in graph.PartToNodes)
        {
            sb.AppendLine("Part " + kvp.Key + " exits: " + kvp.Value.Count);
            for (int i = 0; i < kvp.Value.Count; i++)
            {
                var n = kvp.Value[i];
                sb.AppendLine("  Exit " + n.Exit.exitIndex +
                              " @ " + n.Exit.worldCell +
                              " dir=" + n.Exit.direction +
                              " edges=" + n.Edges.Count);
            }
        }

        // Edge list (no dupes)
        HashSet<string> printed = new HashSet<string>();
        foreach (var nodeEntry in graph.Nodes)
        {
            var from = nodeEntry.Value;
            for (int i = 0; i < from.Edges.Count; i++)
            {
                var edge = from.Edges[i];
                string key = from.Id.ToString() + "->" + edge.To.Id.ToString();
                if (!printed.Add(key)) continue;

                bool internalEdge = from.Part.partId == edge.To.Part.partId;
                string type = internalEdge ? "INT" : "EXT";

                sb.AppendLine("[" + from.Part.partId + ":" + from.Exit.exitIndex + "] -> " +
                              "[" + edge.To.Part.partId + ":" + edge.To.Exit.exitIndex + "]  [" + type + "]");

            }
        }

        Debug.Log(sb.ToString());


        return graph;
    }

    private void DedupeEdges(GraphModel graph)
    {
        foreach (var n in graph.Nodes.Values)
        {
            HashSet<GraphModel.GraphNode> seen = new HashSet<GraphModel.GraphNode>();
            List<GraphModel.GraphEdge> filtered = new List<GraphModel.GraphEdge>();

            for (int i = 0; i < n.Edges.Count; i++)
            {
                var to = n.Edges[i].To;
                if (seen.Add(to))
                    filtered.Add(n.Edges[i]);
            }

            n.Edges.Clear();          // safe with readonly list instance
            n.Edges.AddRange(filtered);
        }
    }
}
