using System;
using System.Collections.Generic;
using System.Linq;
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

    public static Dictionary<string, PathTrackPart> partIdToTrackPart = new Dictionary<string, PathTrackPart>();

    public static PathTrackGraph BuildPathTrackGraph(
    List<PlacedPartInstance> placedParts,
    List<TrackPart> partDefinitions,
    List<PathPartConnection> connections)
    {
        partIdToTrackPart.Clear();
        var graph = new PathTrackGraph();
        var partMap = new Dictionary<string, PathTrackPart>();

        Debug.Log("=== Building PathTrackGraph ===");
        Debug.Log($"Parts count: {placedParts.Count}, Connections count: {connections?.Count ?? 0}");

        foreach (var srcPart in placedParts)
        {
            Debug.Log($"Creating graph node for partId={srcPart.partId}, type={srcPart.partType}, pos={srcPart.position}, rot={srcPart.rotation}");

            // Get the model definition for this part type
            var model = partDefinitions.Find(def => def.partName == srcPart.partType);

            // Directly copy allowedPaths from the definition (no rotation mapping needed)
            
            var allAllowedPaths = new List<AllowedPath>();
            foreach (var group in model.allowedPaths)
                allAllowedPaths.AddRange(group.connections);
            

            // Get occupied cells for this instance using your editor utility
            var occupiedCells2 = TrackLevelEditorWindow.GetOccupiedCells(srcPart,partDefinitions);


            var pathPart = new PathTrackPart
            {
                partId = srcPart.partId,
                spline = (srcPart.splines.Count > 0) ? new List<Vector2>(srcPart.splines[0]) : new List<Vector2>(),
                placedInstance = srcPart,
                allowedPaths = allAllowedPaths,
                exits = new List<PathTrackExit>()
            };

            partIdToTrackPart.Add(srcPart.partId, pathPart);

            // Find the TrackPart definition for this placed part
            
            if (model == null)
            {
                Debug.LogWarning($"TrackPart definition not found for {srcPart.partType}");
                continue;
            }

            // For each defined connection/exit in the model, build a corresponding exit in the graph
            for (int exitIdx = 0; exitIdx < model.connections.Count; exitIdx++)
            {
                var exitDef = model.connections[exitIdx];
                var exit = new PathTrackExit
                {
                    index = exitIdx,
                    // If you want a world position, calculate from gridOffset, origin, rotation
                    position = srcPart.position + RotateOffset(
                        new Vector2Int(exitDef.gridOffset[0], exitDef.gridOffset[1]),
                        srcPart.rotation,
                        model.gridWidth,
                        model.gridHeight)
                };
                pathPart.exits.Add(exit);
            }

            graph.parts.Add(pathPart);
            partMap[srcPart.partId] = pathPart;
        }

        // Wire up connections using connection data
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

    public static Vector2Int RotateOffset(Vector2Int offset, int rotationDegrees, int gridWidth, int gridHeight)
    {
        // Center of part in local grid coordinates
        Vector2 pivot = new Vector2((gridWidth - 1) / 2f, (gridHeight - 1) / 2f);

        // Move offset to pivot
        Vector2 cell = new Vector2(offset.x, offset.y) - pivot;

        // Rotate
        int rot = (rotationDegrees % 360 + 360) % 360;
        Vector2 rotated;
        switch (rot)
        {
            case 0:
                rotated = cell;
                break;
            case 90:
                rotated = new Vector2(-cell.y, cell.x);
                break;
            case 180:
                rotated = new Vector2(-cell.x, -cell.y);
                break;
            case 270:
                rotated = new Vector2(cell.y, -cell.x);
                break;
            default:
                throw new ArgumentException("Rotation must be 0, 90, 180, or 270");
        }

        // Move back from pivot and round to grid
        Vector2 result = rotated + pivot;
        return new Vector2Int(Mathf.RoundToInt(result.x), Mathf.RoundToInt(result.y));
    }

    // Adjusted RotateCell method to handle curved parts correctly for W != H and ensure alignment with the grid
    public static Vector2Int RotateCell(Vector2Int cell, int rotation, int gridWidth, int gridHeight)
    {
        // Check if the grid is square or non-square
        bool isSquare = (gridWidth == gridHeight);

        if (isSquare)
        {
            // Standard rotation for square grids (W == H)
            switch (rotation % 360)
            {
                case 90:
                    return new Vector2Int(-cell.y, cell.x);

                case 180:
                    return new Vector2Int(-cell.x, -cell.y);

                case 270:
                    return new Vector2Int(cell.y, -cell.x);

                default:
                    return cell; // No rotation
            }
        }
        else
        {
            // Special rotation for non-square grids (W != H)
            switch (rotation % 360)
            {
                case 90:
                    return new Vector2Int(gridHeight - cell.y - 1, cell.x);

                case 180:
                    return new Vector2Int(gridWidth - cell.x - 1, gridHeight - cell.y - 1);

                case 270:
                    return new Vector2Int(cell.y, gridWidth - cell.x - 1);

                default:
                    return cell; // No rotation
            }
        }
    }

    // Adjusted BuildConnectionsFromGrid method to ensure proper neighbor detection and fix connections
    public static List<PathPartConnection> BuildConnectionsFromGrid(
    List<PlacedPartInstance> placedParts,
    List<TrackPart> partDefinitions)
    {
        var connections = new List<PathPartConnection>();
        var partByPos = BuildPartPositionMap(placedParts, partDefinitions);

        foreach (var part in placedParts)
        {
            var def = FindTrackPart(partDefinitions, part.partType);
            if (def == null)
            {
                Debug.LogWarning($"No definition found for {part.partType}");
                continue;
            }

            Debug.Log($"PART: {part.partId} ({part.partType}) @ {part.position} rot={part.rotation} - exits in definition: {def.connections.Count}");

            for (int exitIdx = 0; exitIdx < def.connections.Count; exitIdx++)
            {
                var exit = def.connections[exitIdx];
                Vector2Int localCell = new Vector2Int(exit.gridOffset[0], exit.gridOffset[1]);

                // Rotate cell for parts with W != H
                Vector2Int rotatedCell = (def.gridWidth != def.gridHeight)
                    ? RotateCell(localCell, part.rotation, def.gridWidth, def.gridHeight)
                    : localCell;

                Vector2Int worldCell = part.position + rotatedCell;
                int worldDir = (exit.direction + part.rotation / 90) % 4;

                // Correct calculation for neighborCell to ensure it points outward
                Vector2Int neighborCell = worldCell + DirectionToOffset(worldDir);

                // Debugging to ensure neighborCell is correctly calculated
                Debug.Log($"  Testing exit {exitIdx}: localCell={localCell} rotatedCell={rotatedCell} worldCell={worldCell} " +
                          $"worldDir={worldDir} neighborCell={neighborCell} (exit.direction={exit.direction}, part.rotation={part.rotation})");

                

                // Check if neighbor part exists at neighborCell
                if (!CheckNeighborSpanningParts(neighborCell, partByPos, partDefinitions, out var neighborPart))
                {
                    Debug.Log($"    No neighbor at cell {neighborCell} for exit {exitIdx} of {part.partId} ({part.partType})");
                    continue;
                }

                var neighborDef = FindTrackPart(partDefinitions, neighborPart.partType);
                if (neighborDef == null)
                {
                    Debug.LogWarning($"    No definition found for neighbor {neighborPart.partType} at cell {neighborCell}");
                    continue;
                }

                Debug.Log($"    Neighbor {neighborPart.partId} ({neighborPart.partType}) found at {neighborCell}");

                int matchingNeighborExit = -1;
                for (int nIdx = 0; nIdx < neighborDef.connections.Count; nIdx++)
                {
                    var nExit = neighborDef.connections[nIdx];
                    Vector2Int nLocalCell = new Vector2Int(nExit.gridOffset[0], nExit.gridOffset[1]);

                    Vector2Int nRotatedCell = (neighborDef.gridWidth != neighborDef.gridHeight)
                        ? RotateCell(nLocalCell, neighborPart.rotation, neighborDef.gridWidth, neighborDef.gridHeight)
                        : nLocalCell;

                    Vector2Int nWorldCell = neighborPart.position + nRotatedCell;
                    int nWorldDir = (nExit.direction + neighborPart.rotation / 90) % 4;

                    Debug.Log($"    Checking neighbor exit {nIdx}: nLocalCell={nLocalCell} nRotatedCell={nRotatedCell} " +
                              $"nWorldCell={nWorldCell} neighborCell={neighborCell} nWorldDir={nWorldDir} (nExit.direction={nExit.direction}, neighborPart.rotation={neighborPart.rotation})");

                    // Match connection: neighbor exit must point back to the current part's exit
                    if (nWorldCell == neighborCell && nWorldDir == (worldDir + 2) % 4)
                    {
                        matchingNeighborExit = nIdx;
                        Debug.Log($"      MATCH: Neighbor {neighborPart.partId} ({neighborPart.partType}) exit {nIdx} at cell {nWorldCell}, direction {nWorldDir} matches!");
                        break;
                    }
                }

                if (matchingNeighborExit != -1)
                {
                    Debug.Log($"  Exit {exitIdx} on {part.partId} connects to {neighborPart.partId} ({neighborPart.partType}) at exit {matchingNeighborExit}");
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
                    Debug.LogWarning($"  Exit {exitIdx} on {part.partId} ({part.partType}) @ {worldCell} (dir={worldDir}) found neighbor {neighborPart.partId} but no matching exit!");
                }
            }
        }

        Debug.Log("=== Connection Summary ===");
        foreach (var group in placedParts)
        {
            var def = FindTrackPart(partDefinitions, group.partType);
            if (def == null) continue;
            int count = connections.Count(c => c.fromPartId == group.partId);
            Debug.Log($"PART: {group.partId} ({group.partType}) - exits in definition: {def.connections.Count}, connections made: {count}");
        }

        Debug.Log("=== END BuildConnectionsFromGrid ===");
        return connections;
    }

    // Helper method to check spanning parts
    private static bool CheckNeighborSpanningParts(Vector2Int neighborCell, Dictionary<Vector2Int, PlacedPartInstance> partByPos, List<TrackPart> partDefinitions, out PlacedPartInstance neighborPart)
    {
        foreach (var kvp in partByPos)
        {
            PlacedPartInstance part = kvp.Value;
            if (TrackLevelEditorWindow.GetOccupiedCells(part, partDefinitions).Contains(neighborCell)) // Implement OccupiesCell to check span
            {
                neighborPart = part;
                return true;
            }
        }
        neighborPart = null;
        return false;
    }
    // DirectionToOffset helper method remains unchanged
    public static Vector2Int DirectionToOffset(int direction)
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

    

    private static Dictionary<Vector2Int, PlacedPartInstance> BuildPartPositionMap(List<PlacedPartInstance> placedParts, List<TrackPart> partDefinitions)
    {
        var partByPos = new Dictionary<Vector2Int, PlacedPartInstance>();
        foreach (var part in placedParts)
        {
            // Get all occupied cells for this part instance
            var occupiedCells = TrackLevelEditorWindow.GetOccupiedCells(part, partDefinitions);
            foreach (var cell in occupiedCells)
            {
                partByPos[cell] = part;
            }
        }
        return partByPos;
    }


    

}