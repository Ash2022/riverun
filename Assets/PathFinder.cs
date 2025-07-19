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
    public int splineIndex; // NEW: Which spline template to use for this segment

    public PathSegment(PathTrackPart part,PlacedPartInstance placedPart,int entranceExitIdx,int exitIdx,float tStart,float tEnd,int splineIndex // NEW: Pass splineIndex in constructor
    )
    {
        this.part = part;
        this.placedPart = placedPart;
        this.entranceExitIdx = entranceExitIdx;
        this.exitIdx = exitIdx;
        this.tStart = tStart;
        this.tEnd = tEnd;
        this.splineIndex = splineIndex; // NEW
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
    PlacedPartInstance endInstance, int endExitIdx, List<TrackPart> partsLibrary)
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
        return FindPathAnyExit(startPart, endPart, partsLibrary);
    }


    public List<PathSegment> FindPathAnyExit(PathTrackPart startPart, PathTrackPart endPart, List<TrackPart> partsLibrary)
    {
        Debug.Log("=== PATH SEARCH START ===");
        Debug.Log($"FindPathAnyExit: start={startPart.partId} end={endPart.partId}");

        var queue = new Queue<(PathTrackPart currentPart, int entranceIdx, List<PathSegment> path)>();
        var visited = new HashSet<string>();

        Debug.Log($"Start part exits: {startPart.exits?.Count ?? 0}, allowedPaths: {startPart.allowedPaths?.Count ?? 0}");

        TrackPart startPartModel = partsLibrary.Find(p => p.partName == startPart.placedInstance.partType);
        TrackPart endPartModel = partsLibrary.Find(p => p.partName == endPart.placedInstance.partType);

        foreach (var exit in startPart.exits)
        {
            Debug.Log($"  Checking start exit idx={exit.index} connects to part={(exit.connectedPart != null ? exit.connectedPart.partId : "null")}");
            if (exit.connectedPart == null) continue;

            foreach (var allowed in startPart.allowedPaths)
            {
                if (allowed.entryConnectionId == exit.index)
                {
                    int splineIndex = FindSplineIndex(startPartModel, allowed);

                    Debug.Log($"  Enqueue START segment: part={startPart.partId} entrance={allowed.entryConnectionId} exit={allowed.exitConnectionId} connects to={exit.connectedPart.partId} splineIndex={splineIndex}");
                    var segment = new PathSegment(
                        startPart,
                        startPart.placedInstance,
                        allowed.entryConnectionId,
                        allowed.exitConnectionId,
                        0f,
                        1f,
                        splineIndex
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
            Debug.Log($"--- Loop {loopCounter}: At part={currentPart.partId}, entranceIdx={entranceIdx}, pathLen={currentPath.Count}, queueSize={queue.Count} ---");

            if (currentPart == endPart)
            {
                var finalAllowed = endPart.allowedPaths.FirstOrDefault(ap => ap.entryConnectionId == entranceIdx);
                if (finalAllowed != null)
                {
                    int splineIndex = FindSplineIndex(endPartModel, finalAllowed);

                    var finalSegment = new PathSegment(
                        endPart,
                        endPart.placedInstance,
                        finalAllowed.entryConnectionId,
                        finalAllowed.exitConnectionId,
                        0f, 1f,
                        splineIndex
                    );
                    var resultPath = new List<PathSegment>(currentPath) { finalSegment };
                    Debug.Log($"=== PATH FOUND: Reached end part {endPart.partId}. Path length: {resultPath.Count} ===");
                    return resultPath;
                }
                else
                {
                    Debug.LogWarning($"=== PATH INCOMPLETE: Could not find allowed path into endPart {endPart.partId} via entrance {entranceIdx}. Returning partial path. ===");
                    return currentPath;
                }
            }

            TrackPart currentPartModel = partsLibrary.Find(p => p.partName == currentPart.placedInstance.partType);

            foreach (var allowed in currentPart.allowedPaths)
            {
                if (allowed.entryConnectionId != entranceIdx) continue;

                foreach (var exit in currentPart.exits)
                {
                    if (allowed.exitConnectionId != exit.index || exit.connectedPart == null) continue;

                    var visitKey = $"{currentPart.partId}:{allowed.exitConnectionId}";
                    if (visited.Contains(visitKey))
                    {
                        Debug.Log($"    Skipping already visited {visitKey}");
                        continue;
                    }
                    visited.Add(visitKey);

                    int splineIndex = FindSplineIndex(currentPartModel, allowed);

                    Debug.Log($"    Enqueue segment: part={currentPart.partId} entrance={allowed.entryConnectionId} exit={allowed.exitConnectionId} connects to={exit.connectedPart.partId} splineIndex={splineIndex}");

                    var nextSegment = new PathSegment(
                        currentPart,
                        currentPart.placedInstance,
                        allowed.entryConnectionId,
                        allowed.exitConnectionId,
                        0f, 1f,
                        splineIndex
                    );
                    var newPath = new List<PathSegment>(currentPath) { nextSegment };
                    queue.Enqueue((exit.connectedPart, exit.connectedExitIndex, newPath));
                }
            }
        }

        Debug.Log($"=== NO PATH FOUND from {startPart.partId} to {endPart.partId} ===");
        Debug.Log("=== PATH SEARCH END ===");
        return null;
    }



    public static int FindSplineIndex(TrackPart part, AllowedPath allowed)
    {
        if (part == null || allowed == null) return -1;
        for (int groupIdx = 0; groupIdx < part.allowedPaths.Count; groupIdx++)
        {
            var group = part.allowedPaths[groupIdx];
            if (group.connections.Contains(allowed))
                return groupIdx;
        }
        return -1;
    }
}