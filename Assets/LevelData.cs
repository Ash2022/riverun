using System;
using System.Collections.Generic;
using UnityEngine;

// This is your LEVEL INSTANCE. It defines a specific arrangement of part instances and game data.

[Serializable]
public class LevelData
{
    public string levelName;                      // Level name
    public int width, height;                     // Grid dimensions
    public List<PlacedPartInstance> parts;        // All placed part instances
    public GameData gameData;                     // Game-specific info (may be subclassed)
}

// A placed part instance on the grid
[Serializable]
public class PlacedPartInstance
{
    public string partType;                       // Matches TrackPart.partName
    public int partId;                            // Unique per instance (optional)
    public Vector2Int position;                   // Grid position (top-left cell)
    public int rotation;                          // Rotation in degrees (0, 90, etc.)

    // Spline(s) for this part instance (for each allowed path)
    public List<List<Vector2>> splines;           // In local part space (0..w, 0..h)
    // Add other instance-specific data as needed
}

// Game-specific level data (structure is flexible, can be subclassed for each game mode)
[Serializable]
public class GameData
{
    // Example fields for a race game:
    // public Vector2Int start;
    // public Vector2Int end;

    // Example for a puzzle game:
    // public List<Vector2Int> goals;
    // public int movesAllowed;

    // Extend/subclass as needed for each game type!
}