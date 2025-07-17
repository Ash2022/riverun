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
        var log = new StringBuilder();
        log.AppendLine($"PathFinder: Searching from {startPart?.partId}[ANY] to {endPart?.partId}[ANY]\n");

        var visited = new HashSet<(PathTrackPart, int)>();
        var queue = new Queue<List<PathSegment>>();

        // Start from all possible exits on the start part
        foreach (var startExit in startPart.exits)
        {
            queue.Enqueue(new List<PathSegment> {
            new PathSegment(startPart, startExit.index, startExit.index, 0.5f, 0.5f)
        });
            visited.Add((startPart, startExit.index));
        }

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

            // Found the end part, at any exit
            if (part == endPart)
            {
                log.AppendLine($"PathFinder: Path found with {path.Count} segments! Destination reached at exit {exitIdx}");
                foundPath = true;
                found = path;
                break;
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

                // Only visit each part/exit once
                if (!visited.Add((nextPart, nextExitIdx)))
                {
                    log.AppendLine($"  Already visited {nextPart.partId}[{nextExitIdx}], skipping.");
                    continue;
                }

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