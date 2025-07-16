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
    private static int NextID = 1;
    public int gridX;
    public int gridY;
    public GamePointType type;
    public int colorIndex;
    public int id;
    public List<int> waitingPeople = new List<int>(); // Each int is a color index

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

    // Helper: Only show people if station
    public bool HasWaitingPeople => waitingPeople.Count > 0;
}

public class GameModel
{
    public List<GamePoint> points = new List<GamePoint>();
}