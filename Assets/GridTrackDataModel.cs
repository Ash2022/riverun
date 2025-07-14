using System.Collections.Generic;
using UnityEngine;

public enum PointType
{
    Station,
    Junction,
    EndStation
}

[System.Serializable]
public class SpecialPoint
{
    public int id;
    public int col;
    public int row;
    public PointType type;
}

[System.Serializable]
public class PathPair
{
    public int idA;
    public int idB;

    public PathPair() { }

    public PathPair(int a, int b)
    {
        idA = a;
        idB = b;
    }

    public override string ToString()
    {
        return $"{idA} <-> {idB}";
    }

    public override bool Equals(object obj)
    {
        return obj is PathPair pair &&
               ((idA == pair.idA && idB == pair.idB) || (idA == pair.idB && idB == pair.idA));
    }

    public override int GetHashCode()
    {
        int hashCode = 17;
        hashCode = hashCode * 31 + idA.GetHashCode();
        hashCode = hashCode * 31 + idB.GetHashCode();
        return hashCode;
    }
}

[System.Serializable]
public class PathData
{
    public int idA;
    public int idB;
    public List<Vector2Int> cells = new List<Vector2Int>();
}

[System.Serializable]
public class GridTrackDataModel
{
    public List<SpecialPoint> points = new List<SpecialPoint>();
    public List<PathData> paths = new List<PathData>();
    public List<int> mainLoopNodeIds = new List<int>(); // Ordered node IDs for main loop spline

    public int nextPointId = 1;
}