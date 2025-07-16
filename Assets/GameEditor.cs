using System.Collections.Generic;

public class GameEditor
{
    public List<GamePoint> points = new List<GamePoint>();

    public GameEditor(GameModel gameData)
    {
        // Initialize points from gameData if needed
    }

    public void OnGridCellClicked(int gx, int gy, int mouseButton)
    {
        if (mouseButton == 0) // left click to add
        {
            if (!points.Exists(p => p.gridX == gx && p.gridY == gy))
                points.Add(new GamePoint(gx, gy, GamePointType.Station, 0));
        }
        else if (mouseButton == 1) // right click to remove
        {
            points.RemoveAll(p => p.gridX == gx && p.gridY == gy);
        }
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

