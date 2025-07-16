using System.Collections.Generic;
using UnityEngine;

public class CellOccupationManager
{
    public Dictionary<Vector2Int, PlacedPartInstance> cellToPart = new Dictionary<Vector2Int, PlacedPartInstance>();
    private List<TrackPart> partsLibrary;

    public CellOccupationManager(List<TrackPart> partsLibrary)
    {
        this.partsLibrary = partsLibrary;
    }

    // Call this after placing or updating a part
    public void AddOrUpdatePart(PlacedPartInstance instance)
    {
        RemovePart(instance); // Remove old cells first (if moving/rotating)
        TrackPart model = partsLibrary.Find(p => p.partName == instance.partType);
        if (model == null) return;
        int width = (instance.rotation % 180 == 0) ? model.gridWidth : model.gridHeight;
        int height = (instance.rotation % 180 == 0) ? model.gridHeight : model.gridWidth;
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int cell = new Vector2Int(instance.position.x + dx, instance.position.y + dy);
                cellToPart[cell] = instance;
            }
        }
    }

    // Call this BEFORE deleting or moving a part
    public void RemovePart(PlacedPartInstance instance)
    {
        TrackPart model = partsLibrary.Find(p => p.partName == instance.partType);
        if (model == null) return;
        int width = (instance.rotation % 180 == 0) ? model.gridWidth : model.gridHeight;
        int height = (instance.rotation % 180 == 0) ? model.gridHeight : model.gridWidth;
        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int cell = new Vector2Int(instance.position.x + dx, instance.position.y + dy);
                if (cellToPart.ContainsKey(cell) && cellToPart[cell] == instance)
                    cellToPart.Remove(cell);
            }
        }
    }

    // Call this when loading a level
    public void BuildFromLevel(List<PlacedPartInstance> partInstances)
    {
        cellToPart.Clear();
        foreach (var instance in partInstances)
        {
            AddOrUpdatePart(instance);
        }
    }
}