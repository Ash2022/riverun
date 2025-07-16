using System;
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
    private static int NextID = 1; // Static counter

    public int gridX;
    public int gridY;
    public GamePointType type;
    public int colorIndex; // 0 = red, 1 = green, 2 = blue
    public int id;         // Unique integer ID

    public GamePoint(int x, int y, GamePointType type, int colorIndex = 0)
    {
        gridX = x;
        gridY = y;
        this.type = type;
        this.colorIndex = colorIndex;
        this.id = NextID++;
    }

    public string Letter => type switch
    {
        GamePointType.Station => "S",
        GamePointType.DropStation => "D",
        GamePointType.Train => "T",
        _ => "?"
    };
}

public class GameModel
{
    public List<GamePoint> points = new List<GamePoint>();
}