using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor-side manager for placing/cycling/removing GamePoints.
/// </summary>
public class GameEditor
{
    private readonly GameModel _data;
    private readonly CellOccupationManager _cellMgr;
    private readonly int _colorCount;

    public GameEditor(GameModel gameData, CellOccupationManager cellMgr, int colorCount = 3)
    {
        _data = gameData;
        _cellMgr = cellMgr;
        _colorCount = colorCount;
    }

    public List<GamePoint> GetPoints() => _data.points;
    public void SetPoints(List<GamePoint> pts) => _data.points = pts;

    /// <summary>
    /// Handle a click on the grid in “game” mode.
    /// mouseButton: 0=LMB add/cycle color, 1=RMB cycle type, 2=MMB delete
    /// </summary>
    public void OnGridCellClicked(PlacedPartInstance clickedPart,
                                  int gx, int gy,
                                  int mouseButton,
                                  GamePointType selectedType,
                                  int colorIndex = 0)
    {
        var point = _data.points.FirstOrDefault(p => p.gridX == gx && p.gridY == gy);

        if (mouseButton == 0) // Left click
        {
            if (point == null)
            {
                // Add new
                var anchor = BuildAnchor(clickedPart, gx, gy);
                _data.points.Add(new GamePoint(clickedPart, gx, gy, selectedType, colorIndex, anchor));
            }
            else
            {
                // Cycle color
                point.colorIndex = (point.colorIndex + 1) % _colorCount;
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
                _data.points.Remove(point);
            }
        }
    }

    private GamePointType NextType(GamePointType current)
    {
        return current switch
        {
            GamePointType.Station => GamePointType.Depot,
            GamePointType.Depot => GamePointType.Train,
            GamePointType.Train => GamePointType.Station,
            _ => GamePointType.Station
        };
    }

    /// <summary>
    /// Build an Anchor from the clicked cell and part.
    /// For now: choose the closest exit pin on the part (or -1 if none).
    /// </summary>
    private Anchor BuildAnchor(PlacedPartInstance part, int gx, int gy)
    {
        if (part == null || part.exits == null || part.exits.Count == 0)
            return new Anchor { partId = part?.partId ?? "none", exitPin = -1, splineIndex = -1, t = 0f };

        // Find nearest exit pin by Manhattan or Euclidean distance
        var clickCell = new UnityEngine.Vector2Int(gx, gy);
        int bestPin = part.exits[0].exitIndex;
        float bestDist = float.PositiveInfinity;

        foreach (var ex in part.exits)
        {
            float d = UnityEngine.Vector2Int.Distance(clickCell, ex.worldCell);
            if (d < bestDist)
            {
                bestDist = d;
                bestPin = ex.exitIndex;
            }
        }

        return Anchor.FromPin(part.partId, bestPin);
    }

    public void ClearAll(bool resetIds = true)
    {
        _data.points.Clear();
        if (resetIds) GamePoint.ResetIds();
    }
}
