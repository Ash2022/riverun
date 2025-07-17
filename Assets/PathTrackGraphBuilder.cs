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
    public static PathTrackGraph BuildPathTrackGraph(
        List<PlacedPartInstance> placedParts,
        List<PathPartConnection> connections)
    {
        var graph = new PathTrackGraph();
        var partMap = new Dictionary<string, PathTrackPart>();

        Debug.Log("=== Building PathTrackGraph ===");
        Debug.Log($"Parts count: {placedParts.Count}, Connections count: {connections?.Count ?? 0}");

        foreach (var srcPart in placedParts)
        {
            Debug.Log($"Creating graph node for partId={srcPart.partId}, type={srcPart.partType}, pos={srcPart.position}, rot={srcPart.rotation}");
            var pathPart = new PathTrackPart();
            pathPart.partId = srcPart.partId;
            pathPart.spline = (srcPart.splines.Count > 0) ? new List<Vector2>(srcPart.splines[0]) : new List<Vector2>();
            pathPart.placedInstance = srcPart;
            pathPart.exits = new List<PathTrackExit>();

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

        if (connections != null)
        {
            Debug.Log("Wiring up exits using connection data...");
            foreach (var conn in connections)
            {
                Debug.Log($"  Connection: {conn.fromPartId}[{conn.fromExitIndex}] => {conn.toPartId}[{conn.toExitIndex}]");
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
                        Debug.Log($"    Wired {fromPart.partId}[{fromExit.index}] to {toPart.partId}[{conn.toExitIndex}]");
                    }
                    else
                    {
                        Debug.LogWarning($"    Connection index out of bounds for {conn.fromPartId} or {conn.toPartId}");
                    }
                }
                else
                {
                    Debug.LogWarning($"    Part missing for connection: {conn.fromPartId} or {conn.toPartId}");
                }
            }
        }

        // Print graph summary
        Debug.Log("--- PathTrackGraph summary ---");
        foreach (var part in graph.parts)
        {
            Debug.Log($"Part {part.partId}: exits={part.exits.Count}");
            for (int i = 0; i < part.exits.Count; i++)
            {
                var ex = part.exits[i];
                string connStr = ex.connectedPart != null ? $"Connected to {ex.connectedPart.partId}[{ex.connectedExitIndex}]" : "Not connected";
                Debug.Log($"    Exit {i}: {connStr}");
            }
        }

        Debug.Log("=== Graph build complete ===");
        return graph;
    }

    public static List<PathPartConnection> BuildConnectionsFromGrid(
        List<PlacedPartInstance> placedParts,
        List<TrackPart> partDefinitions)
    {
        var connections = new List<PathPartConnection>();
        var partByPos = BuildPartPositionMap(placedParts);

        Debug.Log("=== Building connections from grid ===");
        foreach (var part in placedParts)
        {
            Debug.Log($"Part {part.partId} ({part.partType}) at {part.position}, rot={part.rotation}");
            var def = FindTrackPart(partDefinitions, part.partType);
            if (def == null)
            {
                Debug.LogWarning($"  No definition found for {part.partType}");
                continue;
            }

            for (int exitIdx = 0; exitIdx < def.connections.Count; exitIdx++)
            {
                var exit = def.connections[exitIdx];
                Vector2Int localCell = new Vector2Int(exit.gridOffset[0], exit.gridOffset[1]);
                Vector2Int worldCell = part.position + RotateCell(localCell, part.rotation);
                int worldDir = (exit.direction + part.rotation / 90) % 4;
                Vector2Int neighborCell = worldCell + DirectionToOffset(worldDir);

                Debug.Log($"  Exit {exitIdx}: localCell={localCell}, worldCell={worldCell}, worldDir={worldDir}, neighborCell={neighborCell}");

                if (!partByPos.TryGetValue(neighborCell, out var neighborPart))
                {
                    Debug.Log($"    No neighbor at {neighborCell}");
                    continue;
                }

                var neighborDef = FindTrackPart(partDefinitions, neighborPart.partType);
                if (neighborDef == null)
                {
                    Debug.LogWarning($"    No definition for neighbor {neighborPart.partType}");
                    continue;
                }

                int matchingNeighborExit = -1;
                for (int nIdx = 0; nIdx < neighborDef.connections.Count; nIdx++)
                {
                    var nExit = neighborDef.connections[nIdx];
                    Vector2Int nLocalCell = new Vector2Int(nExit.gridOffset[0], nExit.gridOffset[1]);
                    Vector2Int nWorldCell = neighborPart.position + RotateCell(nLocalCell, neighborPart.rotation);
                    int nWorldDir = (nExit.direction + neighborPart.rotation / 90) % 4;

                    Debug.Log($"    Checking neighbor exit {nIdx}: nLocalCell={nLocalCell}, nWorldCell={nWorldCell}, nWorldDir={nWorldDir}, pairedCell={nWorldCell + DirectionToOffset(nWorldDir)}");

                    if (nWorldCell + DirectionToOffset(nWorldDir) == worldCell &&
                        nWorldDir == (worldDir + 2) % 4)
                    {
                        matchingNeighborExit = nIdx;
                        Debug.Log($"      Found matching neighbor exit {nIdx}");
                        break;
                    }
                }

                if (matchingNeighborExit != -1)
                {
                    Debug.Log($"    Adding connection: {part.partId}[{exitIdx}] <--> {neighborPart.partId}[{matchingNeighborExit}]");
                    connections.Add(new PathPartConnection
                    {
                        fromPartId = part.partId,
                        fromExitIndex = exitIdx,
                        toPartId = neighborPart.partId,
                        toExitIndex = matchingNeighborExit
                    });
                }
                else
                {
                    Debug.Log($"    No matching neighbor exit found for {neighborPart.partId}");
                }
            }
        }
        Debug.Log($"=== BuildConnectionsFromGrid result: {connections.Count} connections built ===");
        foreach (var conn in connections)
        {
            Debug.Log($"  Connection: {conn.fromPartId}[{conn.fromExitIndex}] <--> {conn.toPartId}[{conn.toExitIndex}]");
        }
        return connections;
    }

    // --- Utility Methods ---
    private static TrackPart FindTrackPart(List<TrackPart> partDefinitions, string partType)
    {
        foreach (var def in partDefinitions)
        {
            if (def.partName == partType)
                return def;
        }
        return null;
    }

    private static Dictionary<Vector2Int, PlacedPartInstance> BuildPartPositionMap(List<PlacedPartInstance> placedParts)
    {
        var map = new Dictionary<Vector2Int, PlacedPartInstance>();
        foreach (var part in placedParts)
        {
            map[part.position] = part;
        }
        return map;
    }

    private static Vector2Int RotateCell(Vector2Int cell, int degrees)
    {
        int steps = ((degrees % 360) + 360) % 360 / 90;
        switch (steps)
        {
            case 0: return cell;
            case 1: return new Vector2Int(-cell.y, cell.x);
            case 2: return new Vector2Int(-cell.x, -cell.y);
            case 3: return new Vector2Int(cell.y, -cell.x);
            default: return cell;
        }
    }

    private static Vector2Int DirectionToOffset(int direction)
    {
        switch (direction % 4)
        {
            case 0: return new Vector2Int(0, -1); // Up
            case 1: return new Vector2Int(1, 0);  // Right
            case 2: return new Vector2Int(0, 1);  // Down
            case 3: return new Vector2Int(-1, 0); // Left
            default: return Vector2Int.zero;
        }
    }
}