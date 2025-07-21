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
    public GameModel gameData = new GameModel();                  // Game-specific info (may be subclassed)
}

// A placed part instance on the grid
[Serializable]
public class PlacedPartInstance
{
    public string partType;                       // Matches TrackPart.partName
    public string partId;                         // Unique per instance (optional)
    public Vector2Int position;                   // Grid position (top-left cell)
    public int rotation;                          // Rotation in degrees (0, 90, etc.)

    // Spline(s) for this part instance (for each allowed path)
    public List<List<Vector2>> splines;           // In local part space (0..w, 0..h)

    // Exit details for the current rotation
    public List<ExitDetails> exits;               // List of exits for this part instance

    public List<Vector2Int> occupyingCells;
    public List<AllowedPathGroup> allowedPathsGroup;

    // Define a class to hold exit-specific data
    public class ExitDetails
    {
        public int exitIndex;                     // Exit index (from model.connections)
        public Vector2Int localCell;              // Local exit cell
        public Vector2Int rotatedCell;            // Rotated cell after applying rotation
        public Vector2Int worldCell;              // World cell in the grid
        public int direction;                     // Direction after rotation
        public Vector2Int neighborCell;           // Neighbor cell to search
    }
}

