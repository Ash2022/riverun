using System.Collections.Generic;
using UnityEngine;

public enum GamePointType
{
    Station,
    DropStation,
    Train
}

public class GamePoint
{
    public int gridX;
    public int gridY;
    public GamePointType type;
    public int colorIndex; // 0 = red, 1 = green, 2 = blue

    public GamePoint(int x, int y, GamePointType type, int colorIndex = 0)
    {
        gridX = x;
        gridY = y;
        this.type = type;
        this.colorIndex = colorIndex;
    }
}

public class GameModel
{
    public List<GamePoint> points = new List<GamePoint>();
}