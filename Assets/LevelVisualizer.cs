using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class LevelVisualizer : MonoBehaviour
{
    [Header("Input Grid Data (JSON)")]
    [SerializeField] private TextAsset levelJson;

    [Header("Grid to World Mapping")]
    [SerializeField] int gridCols = 30;
    [SerializeField] int gridRows = 53;
    [SerializeField] float xMin = -3.25f, xMax = 3.25f;
    [SerializeField] float yMin = -5.75f, yMax = 5.75f;

    [Header("Spline Prefab (must have SplineContainer & LoftRoadBehaviour)")]
    [SerializeField] private GameObject splinePrefab;

    [Header("Default Road Width")]
    [SerializeField] private float defaultRoadWidth = 0.1f;

    private List<GameObject> splineObjects = new();
    private GridTrackDataModel levelData;

    public float cellSize = 1.0f;
    public Vector2 gridOrigin = Vector2.zero;

    private void Start()
    {
        Visualize();
    }

    [ContextMenu("Visualize Level")]
    public void Visualize()
    {
        if (levelJson == null)
        {
            Debug.LogWarning("No JSON TextAsset assigned for level data.");
            return;
        }
        if (splinePrefab == null)
        {
            Debug.LogWarning("No Spline Prefab assigned. Assign a prefab with SplineContainer and LoftRoadBehaviour.");
            return;
        }

        levelData = JsonUtility.FromJson<GridTrackDataModel>(levelJson.text);

        // Cleanup old splines
        foreach (var obj in splineObjects)
        {
            if (obj) DestroyImmediate(obj);
        }
        splineObjects.Clear();

        if (levelData == null)
            return;

        // --- 1. Create a spline for the MAIN LOOP if defined ---
        if (levelData.mainLoopCells != null && levelData.mainLoopCells.Count > 1)
        {
            GameObject splineGO = Instantiate(splinePrefab, this.transform);
            splineGO.name = $"MainLoopSpline";
            var splineContainer = splineGO.GetComponent<SplineContainer>();
            if (splineContainer == null)
            {
                Debug.LogError("Spline prefab does not have a SplineContainer component!");
                DestroyImmediate(splineGO);
            }
            else
            {
                splineContainer.Spline.Clear();
                for (int i = 0; i < levelData.mainLoopCells.Count; i++)
                {
                    var cell = levelData.mainLoopCells[i];
                    Vector3 worldPos = GridToWorld(cell.x, cell.y);
                    BezierKnot knot = new BezierKnot(worldPos);
                    splineContainer.Spline.Add(knot);
                    splineContainer.Spline.SetTangentMode(i, TangentMode.AutoSmooth);
                }
                splineContainer.Spline.Closed = IsMainLoopClosed(levelData);

                splineObjects.Add(splineGO);

                var road = splineGO.GetComponent<Unity.Splines.Examples.LoftRoadBehaviour>();
                if (road != null)
                {
                    if (road.Widths != null)
                    {
                        if (road.Widths.Count == 0)
                            road.Widths.Add(new SplineData<float>() { DefaultValue = defaultRoadWidth });
                        else
                            foreach (var wd in road.Widths)
                                wd.DefaultValue = defaultRoadWidth;
                    }
                }
            }
        }

        // --- 2. Create splines for all extra paths ---
        if (levelData.extraPaths != null)
        {
            for (int pIdx = 0; pIdx < levelData.extraPaths.Count; pIdx++)
            {
                var pathData = levelData.extraPaths[pIdx];
                if (pathData.cells == null || pathData.cells.Count < 2)
                    continue;

                GameObject splineGO = Instantiate(splinePrefab, this.transform);
                splineGO.name = $"ExtraPathSpline_{pIdx}";

                var splineContainer = splineGO.GetComponent<SplineContainer>();
                if (splineContainer == null)
                {
                    Debug.LogError("Spline prefab does not have a SplineContainer component!");
                    DestroyImmediate(splineGO);
                    continue;
                }

                splineContainer.Spline.Clear();

                for (int i = 0; i < pathData.cells.Count; i++)
                {
                    var cell = pathData.cells[i];
                    Vector3 worldPos = GridToWorld(cell.x, cell.y);
                    BezierKnot knot = new BezierKnot(worldPos);
                    splineContainer.Spline.Add(knot);
                    splineContainer.Spline.SetTangentMode(i, TangentMode.AutoSmooth);
                }
                splineContainer.Spline.Closed = false;

                splineObjects.Add(splineGO);

                var road = splineGO.GetComponent<Unity.Splines.Examples.LoftRoadBehaviour>();
                if (road != null)
                {
                    if (road.Widths != null)
                    {
                        if (road.Widths.Count == 0)
                            road.Widths.Add(new SplineData<float>() { DefaultValue = defaultRoadWidth });
                        else
                            foreach (var wd in road.Widths)
                                wd.DefaultValue = defaultRoadWidth;
                    }
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (levelData == null) return;

        // Draw main loop if it exists
        if (levelData.mainLoopCells != null && levelData.mainLoopCells.Count > 1)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.15f, 1f);
            for (int i = 0; i < levelData.mainLoopCells.Count - 1; i++)
            {
                Vector3 a = CellToWorld(levelData.mainLoopCells[i]);
                Vector3 b = CellToWorld(levelData.mainLoopCells[i + 1]);
                Gizmos.DrawLine(a, b);
            }
            foreach (var cell in levelData.mainLoopCells)
            {
                Gizmos.DrawSphere(CellToWorld(cell), cellSize * 0.3f);
            }
        }

        // Draw extra paths
        if (levelData.extraPaths != null)
        {
            Gizmos.color = Color.yellow;
            foreach (var path in levelData.extraPaths)
            {
                for (int i = 0; i < path.cells.Count - 1; i++)
                {
                    Vector3 a = CellToWorld(path.cells[i]);
                    Vector3 b = CellToWorld(path.cells[i + 1]);
                    Gizmos.DrawLine(a, b);
                }
                foreach (var cell in path.cells)
                {
                    Gizmos.DrawSphere(CellToWorld(cell), cellSize * 0.2f);
                }
            }
        }

        // Draw special points/nodes
        if (levelData.nodes != null)
        {
            foreach (var node in levelData.nodes)
            {
                Color color = node.type switch
                {
                    PointType.Station => new Color(0.2f, 0.85f, 0.2f, 1f),
                    PointType.Junction => new Color(1f, 0.95f, 0.2f, 1f),
                    PointType.EndStation => new Color(1f, 0.25f, 0.25f, 1f),
                    _ => Color.white
                };
                Gizmos.color = color;
                Gizmos.DrawSphere(CellToWorld(node.cell), cellSize * 0.4f);
            }
        }
    }

    Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize + gridOrigin.x, 0f, cell.y * cellSize + gridOrigin.y);
    }

    public Vector3 GridToWorld(int col, int row)
    {
        int flippedRow = (gridRows - 1) - row;
        float x = Mathf.Lerp(xMin, xMax, gridCols == 1 ? 0.5f : (float)col / (gridCols - 1));
        float z = Mathf.Lerp(yMin, yMax, gridRows == 1 ? 0.5f : (float)flippedRow / (gridRows - 1));
        return new Vector3(x, 0, z); // Use XZ plane!
    }

    // Helper: Is main loop closed? (first and last cell are the same)
    private bool IsMainLoopClosed(GridTrackDataModel data)
    {
        if (data == null || data.mainLoopCells == null || data.mainLoopCells.Count < 2) return false;
        return data.mainLoopCells[0] == data.mainLoopCells[data.mainLoopCells.Count - 1];
    }
}