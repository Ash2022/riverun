using System.Collections.Generic;
using System.Linq;

public class GameEditor
{
    public List<GamePoint> points = new List<GamePoint>();

    public GameEditor(GameModel gameData)
    {
        // Initialize points from gameData if needed
    }

    public void OnGridCellClicked(PlacedPartInstance placedPartInstance, int gx, int gy, int mouseButton, GamePointType selectedType, int colorIndex = 0)
    {
        var point = points.FirstOrDefault(p => p.gridX == gx && p.gridY == gy);

        if (mouseButton == 0) // Left click
        {
            if (point == null)
            {
                points.Add(new GamePoint(placedPartInstance,gx, gy, selectedType, colorIndex));
            }
            else
            {
                point.colorIndex = (point.colorIndex + 1) % 3;
            }
        }
        else if (mouseButton == 1) // Right click - cycle type
        {
            if (point != null)
            {
                point.type = NextType(point.type);
            }
        }
        else if (mouseButton == 2) // Middle click - delete
        {
            if (point != null)
            {
                points.Remove(point);
            }
        }
    }

    // Helper method to cycle type
    private GamePointType NextType(GamePointType current)
    {
        // Assumes 3 types as per your enum
        return current switch
        {
            GamePointType.Station => GamePointType.DropStation,
            GamePointType.DropStation => GamePointType.Train,
            GamePointType.Train => GamePointType.Station,
            _ => GamePointType.Station
        };
    }

    public List<GamePoint> GetPoints()
    {
        return points;
    }

    public void SetPoints(List<GamePoint> points)
    {
        this.points = points;
    }
}

