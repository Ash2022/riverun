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

        // Build a lookup for all paths for fast access by node pairs
        Dictionary<PathPair, List<Vector2Int>> pathLookup = new();
        if (levelData.paths != null)
        {
            foreach (var pd in levelData.paths)
            {
                var pair = new PathPair(pd.idA, pd.idB);
                pathLookup[pair] = pd.cells != null ? new List<Vector2Int>(pd.cells) : new List<Vector2Int>();
            }
        }

        // --- 1. Create a spline for the MAIN LOOP if defined ---
        if (levelData.mainLoopNodeIds != null && levelData.mainLoopNodeIds.Count > 1)
        {
            List<Vector2Int> mainLoopCells = new();
            for (int i = 0; i < levelData.mainLoopNodeIds.Count - 1; i++)
            {
                int idA = levelData.mainLoopNodeIds[i];
                int idB = levelData.mainLoopNodeIds[i + 1];
                var pair = new PathPair(idA, idB);

                // Try both directions for robustness
                List<Vector2Int> segmentCells = null;
                if (pathLookup.TryGetValue(pair, out segmentCells) && segmentCells.Count >= 2)
                {
                    // Ensure segment follows main loop direction
                    var nodeA = GetNodeGrid(levelData, idA);
                    if (!segmentCells[0].Equals(nodeA))
                        segmentCells.Reverse();
                    mainLoopCells.AddRange(segmentCells);
                }
                else
                {
                    // Try reversed pair
                    var reversePair = new PathPair(idB, idA);
                    if (pathLookup.TryGetValue(reversePair, out segmentCells) && segmentCells.Count >= 2)
                    {
                        var nodeA = GetNodeGrid(levelData, idA);
                        if (!segmentCells[0].Equals(nodeA))
                            segmentCells.Reverse();
                        mainLoopCells.AddRange(segmentCells);
                    }
                    else
                    {
                        Debug.LogWarning($"No cell path found for main loop segment {idA} <-> {idB}. Main loop may be incomplete.");
                    }
                }
            }

            // Create spline for main loop if enough cells
            if (mainLoopCells.Count >= 2)
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
                    for (int i = 0; i < mainLoopCells.Count; i++)
                    {
                        var cell = mainLoopCells[i];
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
        }

        // --- 2. Create splines for all branch/side paths (ignore those that are part of main loop) ---
        if (levelData.paths != null)
        {
            HashSet<string> mainLoopSegmentKeys = new();
            // Build keys for segments in main loop
            if (levelData.mainLoopNodeIds != null && levelData.mainLoopNodeIds.Count > 1)
            {
                for (int i = 0; i < levelData.mainLoopNodeIds.Count - 1; i++)
                {
                    int idA = levelData.mainLoopNodeIds[i];
                    int idB = levelData.mainLoopNodeIds[i + 1];
                    mainLoopSegmentKeys.Add($"{Mathf.Min(idA, idB)}_{Mathf.Max(idA, idB)}");
                }
            }

            for (int pIdx = 0; pIdx < levelData.paths.Count; pIdx++)
            {
                var pathData = levelData.paths[pIdx];
                if (pathData.cells == null || pathData.cells.Count < 2)
                    continue;

                // Skip if part of main loop
                string key = $"{Mathf.Min(pathData.idA, pathData.idB)}_{Mathf.Max(pathData.idA, pathData.idB)}";
                if (mainLoopSegmentKeys.Contains(key)) continue;

                GameObject splineGO = Instantiate(splinePrefab, this.transform);
                splineGO.name = $"Spline_{pathData.idA}_{pathData.idB}_{pIdx}";

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

    public Vector3 GridToWorld(int col, int row)
    {
        int flippedRow = (gridRows - 1) - row;
        float x = Mathf.Lerp(xMin, xMax, gridCols == 1 ? 0.5f : (float)col / (gridCols - 1));
        float z = Mathf.Lerp(yMin, yMax, gridRows == 1 ? 0.5f : (float)flippedRow / (gridRows - 1));
        return new Vector3(x, 0, z); // Use XZ plane!
    }

    // Helper: Find grid cell for node id
    private Vector2Int GetNodeGrid(GridTrackDataModel data, int nodeId)
    {
        if (data == null || data.points == null) return Vector2Int.zero;
        var pt = data.points.Find(p => p.id == nodeId);
        if (pt == null) return Vector2Int.zero;
        return new Vector2Int(pt.col, pt.row);
    }

    // Helper: Is main loop closed? (first and last node id are the same)
    private bool IsMainLoopClosed(GridTrackDataModel data)
    {
        if (data == null || data.mainLoopNodeIds == null || data.mainLoopNodeIds.Count < 2) return false;
        return data.mainLoopNodeIds[0] == data.mainLoopNodeIds[data.mainLoopNodeIds.Count - 1];
    }
}