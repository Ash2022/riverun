using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class GridTrackEditorWindow : EditorWindow
{
    private const int gridCols = 30;
    private const int gridRows = 53;
    private const int cellSize = 10;
    private const int cellGap = 2;
    private Vector2 gridOffset = new Vector2(20, 0);

    private GridTrackDataModel data = new();
    private List<Vector2Int> mainLoopKeyPoints = new();
    private List<Vector2Int> extraPathKeyPoints = new();
    private int extraPathStartNodeId = -1, extraPathEndNodeId = -1;

    private bool mainLoopEditMode = false;
    private bool extraPathEditMode = false;

    private float gridZoom = 1.0f;
    private const float minZoom = 0.4f;
    private const float maxZoom = 2.5f;

    private float ZoomedCellSize => cellSize * gridZoom;
    private float ZoomedCellGap => cellGap * gridZoom;

    [MenuItem("Tools/Grid Track Editor")]
    public static void ShowWindow()
    {
        GetWindow<GridTrackEditorWindow>("Grid Track Editor");
    }

    private void OnEnable()
    {
        // Load or reset data if needed
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("New", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("New Track", "Are you sure you want to reset all data?", "Yes", "No"))
            {
                data = new GridTrackDataModel();
                mainLoopKeyPoints.Clear();
                extraPathKeyPoints.Clear();
                mainLoopEditMode = false;
                extraPathEditMode = false;
                Repaint();
            }
        }
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            string path = EditorUtility.SaveFilePanel("Save Grid Track", Application.dataPath, "GridTrackData.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                SaveToJson(path);
            }
        }
        if (GUILayout.Button("Load", GUILayout.Width(100)))
        {
            string path = EditorUtility.OpenFilePanel("Load Grid Track", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                LoadFromJson(path);
                Repaint();
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Label("Grid Track Editor", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mainLoopEditMode, "Main Loop Edit Mode", "Button", GUILayout.Width(150)))
        {
            if (!mainLoopEditMode)
            {
                mainLoopEditMode = true;
                extraPathEditMode = false;
                mainLoopKeyPoints.Clear();
            }
        }
        else
        {
            if (mainLoopEditMode)
            {
                mainLoopEditMode = false;
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(extraPathEditMode ? "Finish Extra Path" : "Add Extra Path", GUILayout.Width(150)))
        {
            if (!extraPathEditMode)
            {
                // Start new extra path
                extraPathEditMode = true;
                extraPathKeyPoints.Clear();
            }
            else
            {
                // Finish and save current extra path
                if (extraPathKeyPoints.Count >= 2)
                {
                    var cells = GenerateFullPath(extraPathKeyPoints);
                    cells = cells.Where(c => !data.mainLoopCells.Contains(c)).ToList();
                    data.extraPaths.Add(new PathData { cells = cells });
                }
                extraPathKeyPoints.Clear();
                extraPathEditMode = false;
                Repaint();
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (mainLoopEditMode)
        {
            GUILayout.Label("Click grid cells to define main loop in order. Click the first cell again to close the loop.", EditorStyles.helpBox);
            if (GUILayout.Button("Clear Main Loop", GUILayout.Width(120)))
            {
                mainLoopKeyPoints.Clear();
                data.mainLoopCells.Clear();
                Repaint();
            }
            GUILayout.Label("Main Loop Key Points: " + string.Join(" → ", mainLoopKeyPoints.Select(p => $"({p.x},{p.y})")));
        }

        if (extraPathEditMode)
        {
            GUILayout.Label("Click start/end cells (must be on main loop), then add intermediate key points.", EditorStyles.helpBox);
            if (GUILayout.Button("Clear Extra Path", GUILayout.Width(120)))
            {
                extraPathKeyPoints.Clear();
                extraPathStartNodeId = -1;
                extraPathEndNodeId = -1;
                Repaint();
            }
            if (GUILayout.Button("Finish & Save Extra Path", GUILayout.Width(160)))
            {
                SaveExtraPath();
            }
            GUILayout.Label("Extra Path Key Points: " + string.Join(" → ", extraPathKeyPoints.Select(p => $"({p.x},{p.y})")));
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Zoom: {gridZoom:F2}", GUILayout.Width(70));
        if (GUILayout.Button("+", GUILayout.Width(30)))
        {
            gridZoom = Mathf.Clamp(gridZoom + 0.1f, minZoom, maxZoom);
            Repaint();
        }
        if (GUILayout.Button("-", GUILayout.Width(30)))
        {
            gridZoom = Mathf.Clamp(gridZoom - 0.1f, minZoom, maxZoom);
            Repaint();
        }
        GUILayout.EndHorizontal();

        Rect lastRect = GUILayoutUtility.GetLastRect();
        gridOffset.y = lastRect.yMax + 10;

        float gridW = gridCols * (ZoomedCellSize + ZoomedCellGap);
        float gridH = gridRows * (ZoomedCellSize + ZoomedCellGap);

        Rect gridRect = new Rect(gridOffset.x, gridOffset.y, gridW, gridH);

        GUI.Box(gridRect, GUIContent.none);

        DrawAllPaths();
        DrawMainLoop();
        DrawExtraPathPreview();

        Handles.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        for (int c = 0; c <= gridCols; c++)
        {
            float x = gridOffset.x + c * (ZoomedCellSize + ZoomedCellGap);
            Handles.DrawLine(new Vector2(x, gridOffset.y), new Vector2(x, gridOffset.y + gridH));
        }
        for (int r = 0; r <= gridRows; r++)
        {
            float y = gridOffset.y + r * (ZoomedCellSize + ZoomedCellGap);
            Handles.DrawLine(new Vector2(gridOffset.x, y), new Vector2(gridOffset.x + gridW, y));
        }

        foreach (var pt in data.nodes)
        {
            Color color = pt.type switch
            {
                PointType.Station => new Color(0.2f, 0.85f, 0.2f, 1f),
                PointType.Junction => new Color(1f, 0.95f, 0.2f, 1f),
                PointType.EndStation => new Color(1f, 0.25f, 0.25f, 1f),
                _ => Color.white
            };
            Rect cellRect = GetCellRect(pt.cell.x, pt.cell.y);
            EditorGUI.DrawRect(cellRect, color);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.black },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 7,
            };
            GUI.Label(cellRect, $"{pt.id}", style);
        }

        HandleGridEvents(gridRect);
    }

    private void HandleGridEvents(Rect gridRect)
    {
        Event e = Event.current;

        if (gridRect.Contains(e.mousePosition) && e.type == EventType.ScrollWheel)
        {
            float delta = -e.delta.y * 0.08f;
            gridZoom = Mathf.Clamp(gridZoom + delta, minZoom, maxZoom);
            Repaint();
            e.Use();
        }

        if (mainLoopEditMode && e.type == EventType.MouseDown && e.button == 0)
        {
            var cell = ScreenToCell(e.mousePosition);
            if (mainLoopKeyPoints.Count == 0)
            {
                mainLoopKeyPoints.Add(cell);
                Repaint();
                e.Use();
                return;
            }
            if (cell == mainLoopKeyPoints[0] && mainLoopKeyPoints.Count >= 3)
            {
                // Close loop
                if (mainLoopKeyPoints.Last() != mainLoopKeyPoints[0])
                    mainLoopKeyPoints.Add(cell);
                // Generate main loop cells
                data.mainLoopCells = GenerateFullPath(mainLoopKeyPoints);
                Repaint();
                e.Use();
                return;
            }
            if (!mainLoopKeyPoints.Contains(cell))
            {
                mainLoopKeyPoints.Add(cell);
                Repaint();
                e.Use();
                return;
            }
        }

        if (extraPathEditMode && e.type == EventType.MouseDown && e.button == 0)
        {
            var cell = ScreenToCell(e.mousePosition);
            // First point MUST be on main loop
            if (extraPathKeyPoints.Count == 0)
            {
                if (data.mainLoopCells.Contains(cell))
                {
                    extraPathKeyPoints.Add(cell);
                    Repaint();
                }
                e.Use();
                return;
            }
            // All subsequent points (end point and intermediates) can be anywhere
            else
            {
                if (!extraPathKeyPoints.Contains(cell))
                {
                    extraPathKeyPoints.Add(cell);
                    Repaint();
                }
                e.Use();
                return;
            }
        }

        // Right click to add/remove nodes
        if (e.type == EventType.MouseDown && e.button == 1)
        {
            if (gridRect.Contains(e.mousePosition))
            {
                var cell = ScreenToCell(e.mousePosition);
                var pt = data.nodes.FirstOrDefault(p => p.cell == cell);
                GenericMenu menu = new GenericMenu();
                if (pt != null)
                {
                    menu.AddItem(new GUIContent($"Delete {pt.type} #{pt.id}"), false, () =>
                    {
                        data.nodes.Remove(pt);
                        Repaint();
                    });
                }
                else
                {
                    menu.AddItem(new GUIContent("Add Station"), false, () => { AddSpecialPoint(PointType.Station, cell); });
                    menu.AddItem(new GUIContent("Add Junction"), false, () => { AddSpecialPoint(PointType.Junction, cell); });
                    menu.AddItem(new GUIContent("Add End Station"), false, () => { AddSpecialPoint(PointType.EndStation, cell); });
                }
                menu.ShowAsContext();
                e.Use();
            }
        }
    }

    private void AddSpecialPoint(PointType type, Vector2Int cell)
    {
        if (data.nodes.Any(p => p.cell == cell)) return;
        data.nodes.Add(new SpecialPoint
        {
            id = data.nextPointId++,
            cell = cell,
            type = type
        });
        Repaint();
    }

    private void SaveExtraPath()
    {
        if (extraPathKeyPoints.Count < 2) return;
        var cells = GenerateFullPath(extraPathKeyPoints);

        // Only remove duplicate cells with main loop EXCEPT for the first cell (start point)
        var filteredCells = new List<Vector2Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (i == 0 || !data.mainLoopCells.Contains(cells[i]))
                filteredCells.Add(cells[i]);
        }

        data.extraPaths.Add(new PathData
        {
            cells = filteredCells,
            startNodeId = FindNodeIdAtCell(extraPathKeyPoints[0]),
            endNodeId = FindNodeIdAtCell(extraPathKeyPoints[1])
        });
        extraPathKeyPoints.Clear();
        extraPathStartNodeId = -1;
        extraPathEndNodeId = -1;
        Repaint();
    }

    private int FindNodeIdAtCell(Vector2Int cell)
    {
        var node = data.nodes.FirstOrDefault(n => n.cell == cell);
        return node != null ? node.id : -1;
    }

    private List<Vector2Int> GenerateFullPath(List<Vector2Int> keyPoints)
    {
        var cells = new List<Vector2Int>();
        for (int i = 0; i < keyPoints.Count - 1; i++)
        {
            var segment = BresenhamLine(keyPoints[i], keyPoints[i + 1]);
            if (i > 0) segment.RemoveAt(0); // Remove duplicate at joint
            cells.AddRange(segment);
        }
        return cells;
    }

    // Bresenham's line algorithm for grid
    public static List<Vector2Int> BresenhamLine(Vector2Int start, Vector2Int end)
    {
        List<Vector2Int> points = new List<Vector2Int>();
        int x0 = start.x, y0 = start.y, x1 = end.x, y1 = end.y;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            points.Add(new Vector2Int(x0, y0));
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
        return points;
    }

    private Rect GetCellRect(int col, int row)
    {
        float x = gridOffset.x + col * (ZoomedCellSize + ZoomedCellGap);
        float y = gridOffset.y + row * (ZoomedCellSize + ZoomedCellGap);
        return new Rect(x + 1, y + 1, ZoomedCellSize - 2, ZoomedCellSize - 2);
    }

    private Vector2Int ScreenToCell(Vector2 mousePos)
    {
        int col = Mathf.FloorToInt((mousePos.x - gridOffset.x) / (ZoomedCellSize + ZoomedCellGap));
        int row = Mathf.FloorToInt((mousePos.y - gridOffset.y) / (ZoomedCellSize + ZoomedCellGap));
        col = Mathf.Clamp(col, 0, gridCols - 1);
        row = Mathf.Clamp(row, 0, gridRows - 1);
        return new Vector2Int(col, row);
    }

    private Vector2 GetCellCenter(int col, int row)
    {
        float x = gridOffset.x + col * (ZoomedCellSize + ZoomedCellGap) + ZoomedCellSize / 2f;
        float y = gridOffset.y + row * (ZoomedCellSize + ZoomedCellGap) + ZoomedCellSize / 2f;
        return new Vector2(x, y);
    }

    private void DrawAllPaths()
    {
        // Draw extra paths
        foreach (var path in data.extraPaths)
        {
            DrawPathCells(path.cells, Color.yellow, 4f);
        }
    }

    private void DrawMainLoop()
    {
        // Draw preview while building
        if (mainLoopEditMode && mainLoopKeyPoints.Count >= 2)
        {
            var previewCells = GenerateFullPath(mainLoopKeyPoints);
            DrawPathCells(previewCells, new Color(1f, 0.7f, 0.25f, 0.6f), 6f); // Preview color/width
        }

        // Draw finalized loop
        if (data.mainLoopCells != null && data.mainLoopCells.Count >= 2)
        {
            DrawPathCells(data.mainLoopCells, new Color(1f, 0.5f, 0.15f, 1f), 8f); // Final color/width
        }
    }

    private void DrawExtraPathPreview()
    {
        // Live preview for extra path
        if (extraPathEditMode && extraPathKeyPoints.Count >= 1)
        {
            foreach (var cell in extraPathKeyPoints)
            {
                Vector2 center = GetCellCenter(cell.x, cell.y);
                Handles.color = new Color(1f, 1f, 0.2f, 0.8f);
                Handles.DrawSolidDisc(center, Vector3.forward, 4f * gridZoom);
            }
            if (extraPathKeyPoints.Count >= 2)
            {
                var previewCells = GenerateFullPath(extraPathKeyPoints);
                DrawPathCells(previewCells, new Color(1f, 1f, 0.1f, 0.8f), 5f); // Preview color/width
            }
        }
    }

    private void DrawPathCells(List<Vector2Int> cells, Color color, float thickness)
    {
        if (cells == null || cells.Count == 0) return;
        Handles.color = color;
        for (int i = 0; i < cells.Count - 1; i++)
        {
            Vector2 from = GetCellCenter(cells[i].x, cells[i].y);
            Vector2 to = GetCellCenter(cells[i + 1].x, cells[i + 1].y);
            Handles.DrawAAPolyLine(thickness, from, to);
        }
        foreach (var cell in cells)
        {
            Vector2 center = GetCellCenter(cell.x, cell.y);
            Handles.DrawSolidDisc(center, Vector3.forward, 2.5f * gridZoom);
        }
    }

    private void SaveToJson(string path)
    {
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        Debug.Log($"Track grid saved to: {path}");
    }

    private void LoadFromJson(string path)
    {
        string json = File.ReadAllText(path);
        data = JsonUtility.FromJson<GridTrackDataModel>(json);
        mainLoopKeyPoints.Clear();
        extraPathKeyPoints.Clear();
        Repaint();
    }
}