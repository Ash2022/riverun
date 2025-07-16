using System;
using System.Collections.Generic;
using UnityEngine;

// This is your PARTS CATALOG. It defines all parts the editor/game can use.

[Serializable]
public class ModelData
{
    public List<TrackPart> parts;
}

// Definition of a single part type (catalog entry)
[Serializable]
public class TrackPart
{
    public string partName;                // Unique name/id
    public int gridWidth, gridHeight;      // Size in grid cells

    public string displaySprite;           // Path to sprite/image

    public List<PartConnection> connections;       // Connection points
    public List<AllowedPath> allowedPaths;         // Allowed paths through this part

    // Spline templates for each allowed path (List of points in local grid space)
    public List<List<Vector2>> splineTemplates;    // Each path has a spline template
}

// Connection point on a part
[Serializable]
public class PartConnection
{
    public int id;                        // Unique per connection
    public int direction;                 // 0=Up, 1=Right, 2=Down, 3=Left
    public int[] gridOffset;              // [x, y] relative to part origin (top-left)
}

// A logical path through a part (between two connections)
[Serializable]
public class AllowedPath
{
    public int entryConnectionId;         // Connection id
    public int exitConnectionId;          // Connection id
    // Can be extended with more info
}