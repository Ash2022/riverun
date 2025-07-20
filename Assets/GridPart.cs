using System;
using UnityEngine;

public enum PartRotation
{
    Degrees0 = 0,
    Degrees90 = 90,
    Degrees180 = 180,
    Degrees270 = 270
}

[System.Serializable]
public class GridPart
{
    [SerializeField] private int gridWidth = 1;
    [SerializeField] private int gridHeight = 1;
    [SerializeField] private Vector2Int position = Vector2Int.zero;
    [SerializeField] private PartRotation rotation = PartRotation.Degrees0;
    
    public int GridWidth => gridWidth;
    public int GridHeight => gridHeight;
    public Vector2Int Position => position;
    public PartRotation Rotation => rotation;
    
    // Get actual dimensions after rotation
    public int ActualWidth => IsVerticalRotation() ? gridHeight : gridWidth;
    public int ActualHeight => IsVerticalRotation() ? gridWidth : gridHeight;
    
    public GridPart(int width, int height, Vector2Int pos, PartRotation rot = PartRotation.Degrees0)
    {
        gridWidth = width;
        gridHeight = height;
        position = pos;
        rotation = rot;
    }
    
    public bool IsVerticalRotation()
    {
        return rotation == PartRotation.Degrees90 || rotation == PartRotation.Degrees270;
    }
    
    public void SetRotation(PartRotation newRotation)
    {
        rotation = newRotation;
    }
    
    public void SetPosition(Vector2Int newPosition)
    {
        position = newPosition;
    }
    
    // Calculate the visual center point for proper pivot placement
    public Vector2 GetVisualCenter()
    {
        float centerX = position.x + (ActualWidth - 1) * 0.5f;
        float centerY = position.y + (ActualHeight - 1) * 0.5f;
        return new Vector2(centerX, centerY);
    }
    
    // Get the grid cells occupied by this part after rotation
    public Vector2Int[] GetOccupiedCells()
    {
        Vector2Int[] cells = new Vector2Int[ActualWidth * ActualHeight];
        int index = 0;
        
        for (int x = 0; x < ActualWidth; x++)
        {
            for (int y = 0; y < ActualHeight; y++)
            {
                cells[index] = new Vector2Int(position.x + x, position.y + y);
                index++;
            }
        }
        
        return cells;
    }
    
    // Transform a local point (relative to part origin) to world grid coordinates
    public Vector2Int TransformLocalToGrid(Vector2Int localPoint)
    {
        Vector2Int transformedPoint = localPoint;
        
        switch (rotation)
        {
            case PartRotation.Degrees90:
                transformedPoint = new Vector2Int(localPoint.y, gridWidth - 1 - localPoint.x);
                break;
            case PartRotation.Degrees180:
                transformedPoint = new Vector2Int(gridWidth - 1 - localPoint.x, gridHeight - 1 - localPoint.y);
                break;
            case PartRotation.Degrees270:
                transformedPoint = new Vector2Int(gridHeight - 1 - localPoint.y, localPoint.x);
                break;
        }
        
        return position + transformedPoint;
    }
    
    // Transform spline points to match the part's rotation
    public Vector3 TransformSplinePoint(Vector3 localSplinePoint, Vector3 worldPos)
    {
        Vector2 center = GetVisualCenter();
        Vector3 worldCenter = new Vector3(center.x, worldPos.y, center.y);
        
        // Translate to origin, rotate, then translate back
        Vector3 relative = worldPos - worldCenter;
        float rotationAngle = (float)rotation * Mathf.Deg2Rad;
        
        float cos = Mathf.Cos(rotationAngle);
        float sin = Mathf.Sin(rotationAngle);
        
        Vector3 rotated = new Vector3(
            relative.x * cos - relative.z * sin,
            relative.y,
            relative.x * sin + relative.z * cos
        );
        
        return worldCenter + rotated;
    }
    
    // Validate if this part can be placed at the given position without overlapping
    public bool CanBePlacedAt(Vector2Int newPos, int gridCols, int gridRows)
    {
        var tempPart = new GridPart(gridWidth, gridHeight, newPos, rotation);
        var cells = tempPart.GetOccupiedCells();
        
        foreach (var cell in cells)
        {
            if (cell.x < 0 || cell.x >= gridCols || cell.y < 0 || cell.y >= gridRows)
                return false;
        }
        
        return true;
    }
}