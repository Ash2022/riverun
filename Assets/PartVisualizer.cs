using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

public class PartVisualizer : MonoBehaviour
{
    [Header("Part Rendering")]
    [SerializeField] private GameObject partPrefab;
    [SerializeField] private Material[] partMaterials;
    
    [Header("Grid to World Mapping")]
    [SerializeField] private float cellSize = 1.0f;
    [SerializeField] private Vector2 gridOrigin = Vector2.zero;
    [SerializeField] private int gridCols = 30;
    [SerializeField] private int gridRows = 53;
    [SerializeField] private float xMin = -3.25f, xMax = 3.25f;
    [SerializeField] private float yMin = -5.75f, yMax = 5.75f;
    
    private List<GameObject> partObjects = new();
    private GridTrackDataModel levelData;
    
    public void VisualizeParts(GridTrackDataModel data)
    {
        levelData = data;
        
        // Clear existing part objects
        foreach (var obj in partObjects)
        {
            if (obj) DestroyImmediate(obj);
        }
        partObjects.Clear();
        
        if (levelData?.parts == null) return;
        
        // Create visual representation for each part
        foreach (var part in levelData.parts)
        {
            CreatePartVisual(part);
        }
    }
    
    private void CreatePartVisual(GridPart part)
    {
        if (partPrefab == null)
        {
            // Create a simple cube representation if no prefab is assigned
            CreateSimplePartVisual(part);
            return;
        }
        
        GameObject partObj = Instantiate(partPrefab, transform);
        partObj.name = $"Part_{part.Position.x}_{part.Position.y}_{part.Rotation}";
        
        // Position the part at its visual center
        Vector2 center = part.GetVisualCenter();
        Vector3 worldPos = GridToWorld(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
        partObj.transform.position = worldPos;
        
        // Apply rotation
        partObj.transform.rotation = Quaternion.Euler(0, (float)part.Rotation, 0);
        
        // Scale based on part dimensions (accounting for rotation)
        Vector3 scale = new Vector3(part.ActualWidth * cellSize, 1f, part.ActualHeight * cellSize);
        partObj.transform.localScale = scale;
        
        partObjects.Add(partObj);
    }
    
    private void CreateSimplePartVisual(GridPart part)
    {
        GameObject partObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        partObj.transform.parent = transform;
        partObj.name = $"Part_{part.Position.x}_{part.Position.y}_{part.Rotation}";
        
        // Position the part at its visual center
        Vector2 center = part.GetVisualCenter();
        Vector3 worldPos = GridToWorld(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
        partObj.transform.position = worldPos;
        
        // Apply rotation
        partObj.transform.rotation = Quaternion.Euler(0, (float)part.Rotation, 0);
        
        // Scale based on part dimensions (accounting for rotation)
        Vector3 scale = new Vector3(part.ActualWidth * cellSize, 0.2f, part.ActualHeight * cellSize);
        partObj.transform.localScale = scale;
        
        // Add visual distinction for parts with W != H
        if (part.GridWidth != part.GridHeight)
        {
            var renderer = partObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.3f, 0.8f, 0.7f); // Purple for unequal dimensions
            }
        }
        
        partObjects.Add(partObj);
    }
    
    public Vector3 GridToWorld(int col, int row)
    {
        int flippedRow = (gridRows - 1) - row;
        float x = Mathf.Lerp(xMin, xMax, gridCols == 1 ? 0.5f : (float)col / (gridCols - 1));
        float z = Mathf.Lerp(yMin, yMax, gridRows == 1 ? 0.5f : (float)flippedRow / (gridRows - 1));
        return new Vector3(x, 0, z);
    }
    
    // Helper method to transform spline points to align with rotated parts
    public Vector3 TransformSplinePointForPart(Vector3 originalPoint, GridPart part)
    {
        if (part == null) return originalPoint;
        return part.TransformSplinePoint(originalPoint, originalPoint);
    }
    
    // Check if a grid position is occupied by any part
    public GridPart GetPartAtPosition(Vector2Int gridPos)
    {
        if (levelData?.parts == null) return null;
        
        foreach (var part in levelData.parts)
        {
            var occupiedCells = part.GetOccupiedCells();
            foreach (var cell in occupiedCells)
            {
                if (cell == gridPos)
                    return part;
            }
        }
        
        return null;
    }
    
    void OnDrawGizmos()
    {
        if (levelData?.parts == null) return;
        
        // Draw part boundaries and rotation indicators
        foreach (var part in levelData.parts)
        {
            Vector2 center = part.GetVisualCenter();
            Vector3 worldCenter = GridToWorld(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.y));
            
            // Draw part boundary
            Gizmos.color = part.GridWidth != part.GridHeight ? 
                new Color(0.8f, 0.3f, 0.8f, 0.5f) : // Purple for W != H
                new Color(0.3f, 0.8f, 0.3f, 0.5f);  // Green for W == H
            
            Vector3 size = new Vector3(part.ActualWidth * cellSize, 0.1f, part.ActualHeight * cellSize);
            Gizmos.matrix = Matrix4x4.TRS(worldCenter, Quaternion.Euler(0, (float)part.Rotation, 0), Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            
            // Draw rotation indicator (arrow pointing forward)
            Gizmos.color = Color.red;
            Vector3 forward = Vector3.forward * (part.ActualHeight * cellSize * 0.3f);
            Gizmos.DrawRay(Vector3.zero, forward);
            Gizmos.DrawCone(forward, Quaternion.identity, 0.1f);
            
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}