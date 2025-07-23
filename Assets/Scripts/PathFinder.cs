using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Dijkstra on RouteModel “states”. Returns your old PathModel.</summary>
public class PathFinder
{
    private RouteModel _model;

    public void Init(RouteModel model) => _model = model;

    public PathModel GetPath(PlacedPartInstance startPart, PlacedPartInstance endPart)
    {
        var log = new StringBuilder();
        log.AppendLine("=== RoutePathFinder ===");
        log.AppendLine($"Start: {startPart.partId}  End: {endPart.partId}");

        // --- build start state set (one per possible entry pin) ---
        var startStates = new List<RouteModel.State>();

        if (_model.parts.TryGetValue(startPart.partId, out var spc) && spc.allowed.Count > 0)
        {
            foreach (var entryPin in spc.allowed.Keys)
                startStates.Add(new RouteModel.State(startPart.partId, entryPin));
        }
        else if (startPart.exits != null && startPart.exits.Count > 0)
        {
            foreach (var ex in startPart.exits)
                startStates.Add(new RouteModel.State(startPart.partId, ex.exitIndex));
        }
        else
        {
            // fallback synthetic entry
            startStates.Add(new RouteModel.State(startPart.partId, -1));
        }

        // goal predicate
        bool IsGoal(RouteModel.State s) => s.partId == endPart.partId;

        // Dijkstra containers
        var dist = new Dictionary<RouteModel.State, float>();
        var prev = new Dictionary<RouteModel.State, PrevRec>();
        var open = new List<RouteModel.State>();
        var closed = new HashSet<RouteModel.State>();

        foreach (var s in startStates)
        {
            dist[s] = 0f;
            open.Add(s);
        }

        var foundGoals = new List<(RouteModel.State s, float c)>();

        // ---- main loop ----
        while (open.Count > 0)
        {
            // extract-min
            RouteModel.State u = default;
            float best = float.PositiveInfinity;
            int idx = -1;
            for (int i = 0; i < open.Count; i++)
            {
                float d = dist[open[i]];
                if (d < best) { best = d; u = open[i]; idx = i; }
            }
            open.RemoveAt(idx);
            if (!closed.Add(u)) continue;

            if (IsGoal(u))
            {
                foundGoals.Add((u, best));
                log.AppendLine($"Reached goal state: {u} cost={best}");
                // don't break – we might still find cheaper goal states
            }

            // expand
            if (!_model.parts.TryGetValue(u.partId, out var pc)) continue;
            if (!pc.allowed.TryGetValue(u.entryPin, out var internalList)) continue;

            for (int i = 0; i < internalList.Count; i++)
            {
                var a = internalList[i];

                if (!pc.neighborByExit.TryGetValue(a.exitPin, out var nb))
                    continue; // dangling exit, ignore

                var v = new RouteModel.State(nb.neighborPartId, nb.neighborPin);
                if (closed.Contains(v)) continue;

                float edgeCost = a.internalLen + nb.externalLen;
                float nd = best + edgeCost;

                if (!dist.TryGetValue(v, out var old) || nd < old)
                {
                    dist[v] = nd;
                    prev[v] = new PrevRec
                    {
                        prevState = u,
                        exitPin = a.exitPin,
                        edgeCost = edgeCost
                    };
                    if (!open.Contains(v)) open.Add(v);
                }
            }
        }

        if (foundGoals.Count == 0)
        {
            log.AppendLine("No path found.");
            Debug.Log(log.ToString());
            return new PathModel(); // failed
        }

        // pick best goal
        foundGoals.Sort((a, b) => a.c.CompareTo(b.c));
        var goal = foundGoals[0].s;
        float totalCost = foundGoals[0].c;

        // reconstruct edge list
        var edgePath = ReconstructEdgePath(goal, prev);

        // dump candidates? (we stored only best)
        log.AppendLine("=== Chosen path ===");
        DumpEdges(edgePath, log);
        log.AppendLine("TotalCost: " + totalCost);
        Debug.Log(log.ToString());

        // build traversal output like before
        var traversals = BuildTraversals(edgePath);

        return new PathModel
        {
            Success = true,
            Traversals = traversals,
            TotalCost = totalCost
        };
    }

    // ---------- internals ----------

    private struct PrevRec
    {
        public RouteModel.State prevState;
        public int exitPin;
        public float edgeCost;
    }

    private List<EdgeStep> ReconstructEdgePath(RouteModel.State goal,
                                               Dictionary<RouteModel.State, PrevRec> prev)
    {
        var list = new List<EdgeStep>();
        var cur = goal;

        while (prev.TryGetValue(cur, out var pr))
        {
            list.Add(new EdgeStep
            {
                from = pr.prevState,
                to = cur,
                exitPin = pr.exitPin,
                cost = pr.edgeCost
            });
            cur = pr.prevState;
        }
        list.Reverse();
        return list;
    }

    private void DumpEdges(List<EdgeStep> steps, StringBuilder sb)
    {
        float sum = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            var e = steps[i];
            sum += e.cost;
            sb.AppendLine($"  {e.from.partId}@in{e.from.entryPin} --[{e.exitPin}]--> {e.to.partId}@in{e.to.entryPin}  cost={e.cost}");
        }
        sb.AppendLine($"  (sum={sum})");
    }

    private List<PathModel.PartTraversal> BuildTraversals(List<EdgeStep> steps)
    {
        var result = new List<PathModel.PartTraversal>();
        if (steps == null || steps.Count == 0) return result;

        int i = 0;
        while (i < steps.Count)
        {
            string curPart = steps[i].from.partId;
            int entryPin = steps[i].from.entryPin;   // how we entered this part
            int exitPin = -1;

            // consume all edges whose FROM is this part
            while (i < steps.Count && steps[i].from.partId == curPart)
            {
                exitPin = steps[i].exitPin;            // the pin we left through on this edge
                if (steps[i].to.partId != curPart)     // we are leaving the part now
                {
                    i++;                               // advance past this edge
                    break;
                }
                i++;
            }

            result.Add(new PathModel.PartTraversal
            {
                partId = curPart,
                entryExit = entryPin,
                exitExit = exitPin
            });
        }

        // Ensure the goal part is present (there is no edge “leaving” it)
        var goalState = steps[steps.Count - 1].to;
        if (result.Count == 0 || result[result.Count - 1].partId != goalState.partId)
        {
            result.Add(new PathModel.PartTraversal
            {
                partId = goalState.partId,
                entryExit = goalState.entryPin,
                exitExit = -1                        // stop inside goal
            });
        }
        else
        {
            // mark last as terminating
            var last = result[result.Count - 1];
            last.exitExit = -1;
            result[result.Count - 1] = last;
        }

        // mark first as synthetic entry
        if (result.Count > 0)
        {
            var first = result[0];
            first.entryExit = -1;
            result[0] = first;
        }

        return result;
    }


    private struct EdgeStep
    {
        public RouteModel.State from;
        public RouteModel.State to;
        public int exitPin;
        public float cost;
    }
}
