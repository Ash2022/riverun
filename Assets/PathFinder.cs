using System.Collections.Generic;

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

// Pathfinding logic
public class PathFinder
{
    private PathTrackGraph graph;

    public PathFinder(PathTrackGraph graph)
    {
        this.graph = graph;
    }

    // Finds a path from startPart/startExit to endPart/endExit
    public List<PathSegment> FindPath(PathTrackPart startPart, int startExitIdx, PathTrackPart endPart, int endExitIdx)
    {
        // Simple BFS pathfinding by exits (can be replaced with A*)
        var visited = new HashSet<(PathTrackPart, int)>();
        var queue = new Queue<List<PathSegment>>();

        // Start with initial segment
        queue.Enqueue(new List<PathSegment> {
            new PathSegment(startPart, startExitIdx, startExitIdx, 0.5f, 0.5f)
        });

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var lastSeg = path[path.Count - 1];
            var part = lastSeg.part;
            var exitIdx = lastSeg.exitIdx;

            if (part == endPart && exitIdx == endExitIdx)
                return path;

            if (!visited.Add((part, exitIdx)))
                continue;

            var exit = part.exits.Find(e => e.index == exitIdx);
            if (exit != null && exit.connectedPart != null)
            {
                var nextPart = exit.connectedPart;
                var nextExitIdx = exit.connectedExitIndex;
                var nextSeg = new PathSegment(nextPart, nextExitIdx, nextExitIdx, 0.5f, 0.5f);
                var newPath = new List<PathSegment>(path) { nextSeg };
                queue.Enqueue(newPath);
            }
        }

        return null; // No path found
    }
}