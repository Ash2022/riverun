using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine; // For Debug.Log

// Represents a leg in the path
public class PathSegment
{
    public PathTrackPart part;
    public PlacedPartInstance placedPart;
    public int entranceExitIdx;
    public int exitIdx;
    public float tStart;
    public float tEnd;

    public PathSegment(PathTrackPart part,PlacedPartInstance placedPart,int entranceExitIdx, int exitIdx, float tStart, float tEnd)
    {
        this.part = part;
        this.placedPart = placedPart;
        this.entranceExitIdx = entranceExitIdx;
        this.exitIdx = exitIdx;
        this.tStart = tStart;
        this.tEnd = tEnd;
    }
}


public class PathFinder
{
    private PathTrackGraph graph;

    public PathFinder(PathTrackGraph graph)
    {
        this.graph = graph;
    }

    public List<PathSegment> FindPath(
    PlacedPartInstance startInstance, int startExitIdx,
    PlacedPartInstance endInstance, int endExitIdx)
    {
        PathTrackPart startPart = null;
        PathTrackPart endPart = null;

        PathTrackGraphBuilder.partIdToTrackPart.TryGetValue(startInstance.partId, out startPart);
        PathTrackGraphBuilder.partIdToTrackPart.TryGetValue(endInstance.partId, out endPart);

        if (startPart == null || endPart == null)
        {
            UnityEngine.Debug.LogWarning("Cannot find PathTrackPart for given PlacedPartInstance.");
            return new List<PathSegment>();
        }

        // Call the original method
        return FindPathAnyExit(startPart, endPart);
    }


    public List<PathSegment> FindPathAnyExit(PathTrackPart startPart, PathTrackPart endPart)
    {
        Debug.Log("FindPathAnyExit: start=" + startPart.partId + " end=" + endPart.partId);

        var queue = new Queue<(PathTrackPart currentPart, int entranceIdx, List<PathSegment> path)>();
        var visited = new HashSet<string>();

        Debug.Log("Start part exits: " + (startPart.exits?.Count ?? 0) + ", allowedPaths: " + (startPart.allowedPaths?.Count ?? 0));

        foreach (var exit in startPart.exits)
        {
            Debug.Log($"Checking start exit idx={exit.index} connects to part={(exit.connectedPart != null ? exit.connectedPart.partId : "null")}");
            if (exit.connectedPart == null) continue;

            foreach (var allowed in startPart.allowedPaths)
            {
                if (allowed.entryConnectionId == exit.index)
                {
                    Debug.Log($"Enqueue segment: startPart={startPart.partId} entrance={allowed.entryConnectionId} exit={allowed.exitConnectionId} connects to={exit.connectedPart.partId}");
                    var segment = new PathSegment(
                        startPart,
                        startPart.placedInstance,
                        allowed.entryConnectionId,
                        allowed.exitConnectionId,
                        0f,
                        1f
                    );
                    var path = new List<PathSegment> { segment };
                    queue.Enqueue((exit.connectedPart, exit.connectedExitIndex, path));
                    visited.Add($"{startPart.partId}:{allowed.exitConnectionId}");
                }
            }
        }

        int loopCounter = 0;
        while (queue.Count > 0)
        {
            loopCounter++;
            var (currentPart, entranceIdx, currentPath) = queue.Dequeue();
            Debug.Log($"Loop {loopCounter}: At part={currentPart.partId}, entranceIdx={entranceIdx}, pathLen={currentPath.Count}");

            if (currentPart == endPart)
            {
                // FIX: Append final segment for arrival at endPart
                var finalAllowed = endPart.allowedPaths.FirstOrDefault(ap => ap.entryConnectionId == entranceIdx);
                if (finalAllowed != null)
                {
                    var finalSegment = new PathSegment(
                        endPart,
                        endPart.placedInstance,
                        finalAllowed.entryConnectionId,
                        finalAllowed.exitConnectionId,
                        0f, 1f
                    );
                    var resultPath = new List<PathSegment>(currentPath) { finalSegment };
                    Debug.Log($"Reached end part: {endPart.partId}. Path length: {resultPath.Count}");
                    return resultPath;
                }
                else
                {
                    Debug.LogWarning($"Could not find allowed path into endPart {endPart.partId} via entrance {entranceIdx}.");
                    return currentPath; // fallback, but not ideal
                }
            }

            foreach (var allowed in currentPart.allowedPaths)
            {
                if (allowed.entryConnectionId != entranceIdx) continue;

                foreach (var exit in currentPart.exits)
                {
                    if (allowed.exitConnectionId != exit.index || exit.connectedPart == null) continue;

                    var visitKey = $"{currentPart.partId}:{allowed.exitConnectionId}";
                    if (visited.Contains(visitKey))
                    {
                        Debug.Log($"  Skipping already visited {visitKey}");
                        continue;
                    }
                    visited.Add(visitKey);

                    Debug.Log($"  Enqueue next segment: part={currentPart.partId} entrance={allowed.entryConnectionId} exit={allowed.exitConnectionId} connects to={exit.connectedPart.partId}");

                    var nextSegment = new PathSegment(
                        currentPart,
                        currentPart.placedInstance,
                        allowed.entryConnectionId,
                        allowed.exitConnectionId,
                        0f, 1f
                    );
                    var newPath = new List<PathSegment>(currentPath) { nextSegment };
                    queue.Enqueue((exit.connectedPart, exit.connectedExitIndex, newPath));
                }
            }
        }

        Debug.LogWarning("No path found from " + startPart.partId + " to " + endPart.partId);
        return null;
    }
}