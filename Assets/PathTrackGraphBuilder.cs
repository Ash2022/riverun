using System.Collections.Generic;
using UnityEngine;

// Connection data between parts
public class PathPartConnection
{
    public string fromPartId;
    public int fromExitIndex;
    public string toPartId;
    public int toExitIndex;
}

public static class PathTrackGraphBuilder
{
    // Builds PathTrackGraph from placed part instances and connection data
    public static PathTrackGraph BuildPathTrackGraph(
        List<PlacedPartInstance> placedParts,
        List<PathPartConnection> connections)
    {
        var graph = new PathTrackGraph();

        // Map partId to PathTrackPart
        var partMap = new Dictionary<string, PathTrackPart>();

        // Instantiate PathTrackParts from PlacedPartInstance
        foreach (var srcPart in placedParts)
        {
            var pathPart = new PathTrackPart();
            pathPart.partId = srcPart.partId;

            // Choose first spline for visualization (you can expand this!)
            pathPart.spline = (srcPart.splines.Count > 0)
                ? new List<Vector2>(srcPart.splines[0])
                : new List<Vector2>();

            pathPart.exits = new List<PathTrackExit>();

            // Add exits; for simplicity, assume one exit per spline endpoint
            // If you have more complex exit data, adapt here!
            var spline = pathPart.spline;
            if (spline.Count > 0)
            {
                var startExit = new PathTrackExit();
                startExit.index = 0;
                startExit.position = spline[0];
                pathPart.exits.Add(startExit);

                var endExit = new PathTrackExit();
                endExit.index = 1;
                endExit.position = spline[spline.Count - 1];
                pathPart.exits.Add(endExit);
            }

            graph.parts.Add(pathPart);
            partMap[srcPart.partId] = pathPart;
        }

        // Wire up exits using connection data
        if (connections != null)
        {
            foreach (var conn in connections)
            {
                if (partMap.ContainsKey(conn.fromPartId) && partMap.ContainsKey(conn.toPartId))
                {
                    var fromPart = partMap[conn.fromPartId];
                    var toPart = partMap[conn.toPartId];

                    if (conn.fromExitIndex >= 0 && conn.fromExitIndex < fromPart.exits.Count &&
                        conn.toExitIndex >= 0 && conn.toExitIndex < toPart.exits.Count)
                    {
                        var fromExit = fromPart.exits[conn.fromExitIndex];
                        fromExit.connectedPart = toPart;
                        fromExit.connectedExitIndex = conn.toExitIndex;
                    }
                }
            }
        }

        return graph;
    }
}