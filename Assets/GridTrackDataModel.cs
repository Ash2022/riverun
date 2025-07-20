using System.Collections.Generic;
using UnityEngine;

public enum PointType { Station, Junction, EndStation }

[System.Serializable]
public class SpecialPoint
{
    public int id;
    public Vector2Int cell;
    public PointType type;
}

[System.Serializable]
public class PathData
{
    public List<Vector2Int> cells = new();
    public int startNodeId; // optional: node id at start
    public int endNodeId;   // optional: node id at end
}

[System.Serializable]
public class GridTrackDataModel
{
    public List<Vector2Int> mainLoopCells = new();
    public List<PathData> extraPaths = new();
    public List<SpecialPoint> nodes = new();
    public List<GridPart> parts = new();
    public int nextPointId = 1;
}