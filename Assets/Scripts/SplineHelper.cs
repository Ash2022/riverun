using System.Collections.Generic;
using UnityEngine;

public static class SplineHelper
{
    /// <summary>
    /// Copies and transforms splines from the TrackPart definition
    /// into the PlacedPartInstance, applying rotation and position.
    /// Call this for every part after loading a level.
    /// </summary>
    public static void CopySplinesToPlacedParts(List<PlacedPartInstance> placedParts, List<TrackPart> partsLibrary)
    {
        // Create a lookup for part definitions by name for speed
        var partDefByName = new Dictionary<string, TrackPart>();
        foreach (var def in partsLibrary)
        {
            partDefByName[def.partName] = def;
        }

        foreach (var placed in placedParts)
        {
            if (!partDefByName.TryGetValue(placed.partType, out var partDef))
            {
                Debug.LogWarning($"TrackPart definition not found for type: {placed.partType}");
                continue;
            }

            placed.splines = CopyAndTransformSplines(partDef, placed.position, placed.rotation);
        }
    }

    /// <summary>
    /// Copies and transforms the spline templates from a TrackPart
    /// with rotation and position applied.
    /// </summary>
    public static List<List<Vector2>> CopyAndTransformSplines(TrackPart part, Vector2Int position, int rotation)
    {
        var result = new List<List<Vector2>>();
        if (part.splineTemplates == null)
            return result;

        foreach (var spline in part.splineTemplates)
        {
            var newSpline = new List<Vector2>();
            foreach (var ptArr in spline)
            {
                Vector2 pt = new Vector2(ptArr[0], ptArr[1]);
                Vector2 rotated = RotateSplinePoint(pt, part.gridWidth, part.gridHeight, rotation);
                newSpline.Add(rotated + new Vector2(position.x, position.y));
            }
            result.Add(newSpline);
        }
        return result;
    }

    /// <summary>
    /// Rotates a spline point around the anchor/top-left according to the part's rotation.
    /// </summary>
    public static Vector2 RotateSplinePoint(Vector2 pt, int width, int height, int rotation)
    {
        switch (rotation % 360)
        {
            case 0: return pt;
            case 90: return new Vector2(height - pt.y - 1, pt.x);
            case 180: return new Vector2(width - pt.x - 1, height - pt.y - 1);
            case 270: return new Vector2(pt.y, width - pt.x - 1);
            default: return pt;
        }
    }
}