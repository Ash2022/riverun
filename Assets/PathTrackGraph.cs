using System.Collections.Generic;
using UnityEngine;

// Represents an exit/connection point on a track part
public class PathTrackExit
{
    public int index; // Unique index for this exit on the part
    public Vector2 position; // Exit position in world space
    public PathTrackPart connectedPart; // The part this exit connects to (if any)
    public int connectedExitIndex; // The index of the exit on the connected part
}

// Represents a track segment (could be straight, curve, etc)
public class PathTrackPart
{
    public PlacedPartInstance placedInstance;
    public string partId;
    public List<Vector2> spline; // The path of the track
    public List<PathTrackExit> exits; // Connection points

    /// <summary>
    /// Allowed entry-to-exit transitions for this part.
    /// Use the AllowedPath type from ModelData for consistency.
    /// </summary>
    public List<AllowedPath> allowedPaths;

    /// <summary>
    /// Grid cells occupied by this part.
    /// Precomputed when building the graph.
    /// </summary>
    public List<Vector2Int> occupiedCells;

    /// <summary>
    /// Utility: Get position on spline from t.
    /// </summary>
    public Vector2 GetPositionOnSpline(float t)
    {
        if (spline == null || spline.Count == 0) return Vector2.zero;
        if (spline.Count < 2) return spline[0];
        float segFloat = t * (spline.Count - 1);
        int seg = Mathf.FloorToInt(segFloat);
        float localT = segFloat - seg;
        if (seg >= spline.Count - 1) return spline[spline.Count - 1];
        return Vector2.Lerp(spline[seg], spline[seg + 1], localT);
    }

    /// <summary>
    /// Checks if traversal from entryExitId to exitExitId is allowed.
    /// </summary>
    public bool IsAllowedTransition(int entryExitId, int exitExitId)
    {
        if (allowedPaths == null)
            return true; // All transitions allowed if not specified

        foreach (var conn in allowedPaths)
        {
            if (conn.entryConnectionId == entryExitId && conn.exitConnectionId == exitExitId)
                return true;
        }
        return false;
    }
}

// Holds all parts and connections
public class PathTrackGraph
{
    public List<PathTrackPart> parts = new List<PathTrackPart>();

    
}