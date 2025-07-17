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

    // Utility: Get position on spline from t
    public Vector2 GetPositionOnSpline(float t)
    {
        if (spline.Count < 2) return spline[0];
        float segFloat = t * (spline.Count - 1);
        int seg = Mathf.FloorToInt(segFloat);
        float localT = segFloat - seg;
        if (seg >= spline.Count - 1) return spline[spline.Count - 1];
        return Vector2.Lerp(spline[seg], spline[seg + 1], localT);
    }
}

// Holds all parts and connections
public class PathTrackGraph
{
    public List<PathTrackPart> parts = new List<PathTrackPart>();

    
}