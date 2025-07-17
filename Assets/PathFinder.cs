using System.Collections.Generic;
using System.Text;
using UnityEngine; // For Debug.Log

// Represents a leg in the path
public class PathSegment
{
    public PathTrackPart part;
    public int entranceExitIdx;
    public int exitIdx;
    public float tStart;
    public float tEnd;

    public PathSegment(PathTrackPart part, int entranceExitIdx, int exitIdx, float tStart, float tEnd)
    {
        this.part = part;
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

        // Suppose you have access to the full list of all path parts:
        foreach (var part in graph.parts) // allParts: List<PathTrackPart>
        {
            if (part.placedInstance.partId == startInstance.partId)
                startPart = part;
            if (part.placedInstance.partId == endInstance.partId)
                endPart = part;
        }

        if (startPart == null || endPart == null)
        {
            UnityEngine.Debug.LogWarning("Cannot find PathTrackPart for given PlacedPartInstance.");
            return new List<PathSegment>();
        }

        // Call the original method
        return FindPath(startPart, startExitIdx, endPart, endExitIdx);
    }

    public List<PathSegment> FindPath(PathTrackPart startPart, int startExitIdx, PathTrackPart endPart, int endExitIdx)
    {
        var log = new StringBuilder();
        log.AppendLine($"PathFinder: Searching from {startPart?.partId}[{startExitIdx}] to {endPart?.partId}[{endExitIdx}]\n");

        var visited = new HashSet<(PathTrackPart, int)>();
        var queue = new Queue<List<PathSegment>>();

        queue.Enqueue(new List<PathSegment> {
            new PathSegment(startPart, startExitIdx, startExitIdx, 0.5f, 0.5f)
        });

        int searchStep = 0;
        bool foundPath = false;
        List<PathSegment> found = null;

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var lastSeg = path[path.Count - 1];
            var part = lastSeg.part;
            var exitIdx = lastSeg.exitIdx;

            log.AppendLine($"Step {searchStep++}: At part {part.partId}, exit {exitIdx}, pathLen={path.Count}");

            if (part == endPart && exitIdx == endExitIdx)
            {
                log.AppendLine($"PathFinder: Path found with {path.Count} segments!");
                foundPath = true;
                found = path;
                break;
            }

            if (!visited.Add((part, exitIdx)))
            {
                log.AppendLine($"  Already visited {part.partId}[{exitIdx}], skipping.");
                continue;
            }

            foreach (var exit in part.exits)
            {
                if (exit.connectedPart == null)
                {
                    log.AppendLine($"  Exit {exit.index} in part {part.partId} not connected to any part.");
                    continue;
                }
                var nextPart = exit.connectedPart;
                var nextExitIdx = exit.connectedExitIndex;

                log.AppendLine($"  Following connection: {part.partId}[{exit.index}] --> {nextPart.partId}[{nextExitIdx}]");

                var nextSeg = new PathSegment(nextPart, nextExitIdx, nextExitIdx, 0.5f, 0.5f);
                var newPath = new List<PathSegment>(path) { nextSeg };
                queue.Enqueue(newPath);
            }
        }

        if (!foundPath)
            log.AppendLine("PathFinder: No path found!");

        Debug.Log(log.ToString());
        return found;
    }
}