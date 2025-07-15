using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class LevelData
{
    public string levelName;
    public int width;
    public int height;
    public List<LevelPartPlacement> parts;
    public Vector2Int start;
    public Vector2Int end;
}

[System.Serializable]
public class LevelPartPlacement
{
    public string partType;
    public int partId;
    public Vector2Int position;
    public int rotation;
}