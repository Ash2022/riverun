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
    private Dictionary<PathPair, List<Vector2Int>> paths = new();
    private PathPair selectedPathPair = null;

    private bool showContextMenu = false;
    private Vector2Int contextMenuCell;
    private Vector2 contextMenuPos;
    private SpecialPoint contextPoint = null;

    private SpecialPoint hoveredPoint = null;
    private Vector2 hoveredPointMousePos;

    private SpecialPoint draggingPoint = null;
    private Vector2 draggingPointOffset;
    private int draggingPathCellIndex = -1;
    private Vector2 draggingPathCellOffset;

    private float gridZoom = 1.0f;
    private const float minZoom = 0.4f;
    private const float maxZoom = 2.5f;

    private bool mainLoopEditMode = false;

    private float ZoomedCellSize => cellSize * gridZoom;
    private float ZoomedCellGap => cellGap * gridZoom;

    [MenuItem("Tools/Grid Track Editor")]
    public static void ShowWindow()
    {
        GetWindow<GridTrackEditorWindow>("Grid Track Editor");
    }

    private void OnEnable()
    {
        RefreshRuntimePaths();
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear All", GUILayout.Width(100)))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to clear all points and paths?", "Yes", "No"))
            {
                data = new GridTrackDataModel();
                paths.Clear();
                selectedPathPair = null;
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

        if (GUILayout.Button("Clear All Paths", GUILayout.Width(120)))
        {
            ClearAllPaths();
        }

        GUILayout.Label("Grid Track Editor", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mainLoopEditMode, "Main Loop Edit Mode", "Button", GUILayout.Width(150)))
        {
            if (!mainLoopEditMode)
            {
                mainLoopEditMode = true;
                selectedPathPair = null;
            }
        }
        else
        {
            if (mainLoopEditMode)
            {
                mainLoopEditMode = false;
            }
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (mainLoopEditMode)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Click nodes in order to define the main loop. Click the starting node again to close the loop.", EditorStyles.helpBox);
            if (GUILayout.Button("Clear Main Loop", GUILayout.Width(120)))
            {
                data.mainLoopNodeIds.Clear();
                Repaint();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Main Loop: " + string.Join(" → ", data.mainLoopNodeIds), EditorStyles.miniBoldLabel);
        }
        else
        {
            DrawPathDropdown();

            if (selectedPathPair != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Draw the path by clicking grid cells. Cells are added in order.", EditorStyles.helpBox);
                if (GUILayout.Button("Clear Path", GUILayout.Width(100)))
                {
                    paths[selectedPathPair] = new List<Vector2Int>();
                    UpdateDataModelPaths();
                    Repaint();
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Right-click on the grid to add or delete a Station, Junction, or End Station.\nHover over a point to see info.", EditorStyles.helpBox);
            }
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

        foreach (var pt in data.points)
        {
            Color color = pt.type switch
            {
                PointType.Station => new Color(0.2f, 0.85f, 0.2f, 1f),
                PointType.Junction => new Color(1f, 0.95f, 0.2f, 1f),
                PointType.EndStation => new Color(1f, 0.25f, 0.25f, 1f),
                _ => Color.white
            };
            Rect cellRect = GetCellRect(pt.col, pt.row);
            EditorGUI.DrawRect(cellRect, color);

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.black },
                alignment = TextAnchor.MiddleCenter,
                fontSize = 7,
            };
            GUI.Label(cellRect, $"{pt.id}", style);
        }

        if (draggingPoint != null)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2Int snapCell = ScreenToCell(mousePos - draggingPointOffset);
            Rect cellRect = GetCellRect(snapCell.x, snapCell.y);
            EditorGUI.DrawRect(cellRect, new Color(0.2f, 0.85f, 0.2f, 0.6f));
            Repaint();
        }
        else if (draggingPathCellIndex != -1 && selectedPathPair != null)
        {
            Vector2 mousePos = Event.current.mousePosition;
            Vector2Int snapCell = ScreenToCell(mousePos - draggingPathCellOffset);
            Rect cellRect = GetCellRect(snapCell.x, snapCell.y);
            EditorGUI.DrawRect(cellRect, new Color(0.1f, 0.6f, 1f, 0.6f));
            Repaint();
        }

        HandleGridEvents(gridRect);

        if (hoveredPoint != null)
        {
            DrawHoverInfo(hoveredPoint, hoveredPointMousePos);
        }

        if (showContextMenu)
        {
            ShowContextMenu();
        }
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

        hoveredPoint = null;
        if (gridRect.Contains(e.mousePosition))
        {
            var cell = ScreenToCell(e.mousePosition);
            hoveredPoint = data.points.FirstOrDefault(p => p.col == cell.x && p.row == cell.y);
            hoveredPointMousePos = e.mousePosition;
        }

        if (mainLoopEditMode && e.type == EventType.MouseDown && e.button == 0 && hoveredPoint != null)
        {
            if (!data.mainLoopNodeIds.Any())
            {
                data.mainLoopNodeIds.Add(hoveredPoint.id);
                Repaint();
                e.Use();
                return;
            }
            else
            {
                if (hoveredPoint.id == data.mainLoopNodeIds[0] && data.mainLoopNodeIds.Count > 2)
                {
                    Repaint();
                    e.Use();
                    return;
                }
                if (!data.mainLoopNodeIds.Contains(hoveredPoint.id))
                {
                    data.mainLoopNodeIds.Add(hoveredPoint.id);
                    Repaint();
                    e.Use();
                    return;
                }
            }
        }

        if (!mainLoopEditMode && selectedPathPair == null)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && hoveredPoint != null)
            {
                draggingPoint = hoveredPoint;
                draggingPointOffset = e.mousePosition - GetCellCenter(draggingPoint.col, draggingPoint.row);
                e.Use();
            }
            if (e.type == EventType.MouseDrag && draggingPoint != null)
            {
                Repaint();
                e.Use();
            }
            if (e.type == EventType.MouseUp && draggingPoint != null)
            {
                Vector2 mousePos = e.mousePosition;
                Vector2Int snapCell = ScreenToCell(mousePos - draggingPointOffset);
                if (!data.points.Any(p => p != draggingPoint && p.col == snapCell.x && p.row == snapCell.y))
                {
                    draggingPoint.col = snapCell.x;
                    draggingPoint.row = snapCell.y;
                }
                draggingPoint = null;
                Repaint();
                e.Use();
            }
        }

        if (!mainLoopEditMode && selectedPathPair != null && paths.TryGetValue(selectedPathPair, out var pathCells))
        {
            int hoverPathCellIdx = -1;
            if (gridRect.Contains(e.mousePosition) && pathCells != null)
            {
                Vector2Int cell = ScreenToCell(e.mousePosition);
                for (int i = 0; i < pathCells.Count; i++)
                {
                    if (pathCells[i] == cell)
                    {
                        hoverPathCellIdx = i;
                        break;
                    }
                }
            }
            if (e.type == EventType.MouseDown && e.button == 0 && hoverPathCellIdx != -1)
            {
                draggingPathCellIndex = hoverPathCellIdx;
                draggingPathCellOffset = e.mousePosition - GetCellCenter(pathCells[draggingPathCellIndex].x, pathCells[draggingPathCellIndex].y);
                e.Use();
            }
            if (e.type == EventType.MouseDrag && draggingPathCellIndex != -1)
            {
                Repaint();
                e.Use();
            }
            if (e.type == EventType.MouseUp && draggingPathCellIndex != -1)
            {
                Vector2 mousePos = e.mousePosition;
                Vector2Int snapCell = ScreenToCell(mousePos - draggingPathCellOffset);
                if (!pathCells.Contains(snapCell))
                {
                    pathCells[draggingPathCellIndex] = snapCell;
                    UpdateDataModelPaths();
                }
                draggingPathCellIndex = -1;
                Repaint();
                e.Use();
            }
        }

        if (!mainLoopEditMode && selectedPathPair != null && e.type == EventType.MouseDown && e.button == 0)
        {
            if (gridRect.Contains(e.mousePosition) && draggingPathCellIndex == -1)
            {
                var cell = ScreenToCell(e.mousePosition);
                var path = paths[selectedPathPair];
                if (!path.Contains(cell))
                {
                    path.Add(cell);
                    UpdateDataModelPaths();
                    Repaint();
                }
                e.Use();
            }
        }

        if (!mainLoopEditMode && e.type == EventType.MouseDown && e.button == 1 && !showContextMenu && selectedPathPair == null)
        {
            if (gridRect.Contains(e.mousePosition))
            {
                var cell = ScreenToCell(e.mousePosition);
                contextMenuCell = cell;
                contextMenuPos = e.mousePosition;
                contextPoint = data.points.FirstOrDefault(p => p.col == cell.x && p.row == cell.y);
                showContextMenu = true;
                e.Use();
            }
        }

        if (showContextMenu && e.type == EventType.MouseDown && e.button == 0)
        {
            if (new Rect(contextMenuPos, new Vector2(160, contextPoint != null ? 65 : 75)).Contains(e.mousePosition) == false)
                showContextMenu = false;
        }
    }

    private void DrawPathDropdown()
    {
        List<PathPair> allPairs = new List<PathPair>();
        for (int i = 0; i < data.points.Count; i++)
            for (int j = i + 1; j < data.points.Count; j++)
                allPairs.Add(new PathPair(data.points[i].id, data.points[j].id));

        string[] options = new string[allPairs.Count + 1];
        options[0] = "[Add/Edit Special Points]";
        for (int i = 0; i < allPairs.Count; i++)
            options[i + 1] = allPairs[i].ToString();

        int selIndex = 0;
        if (selectedPathPair != null)
        {
            int idx = allPairs.IndexOf(selectedPathPair);
            if (idx >= 0) selIndex = idx + 1;
        }

        int newSelIndex = EditorGUILayout.Popup("Edit Path:", selIndex, options);
        if (newSelIndex != selIndex)
        {
            if (newSelIndex == 0)
                selectedPathPair = null;
            else
            {
                selectedPathPair = allPairs[newSelIndex - 1];
                if (!paths.ContainsKey(selectedPathPair))
                    paths[selectedPathPair] = new List<Vector2Int>();
                UpdateDataModelPaths();
            }
        }
    }

    private void ShowContextMenu()
    {
        float height = contextPoint != null ? 65 : 75;
        Rect menuRect = new Rect(contextMenuPos.x, contextMenuPos.y, 160, height);
        GUI.Box(menuRect, "");
        GUILayout.BeginArea(menuRect);

        if (contextPoint != null)
        {
            var labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.black },
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            GUILayout.Label($"{contextPoint.type} #{contextPoint.id}", labelStyle);
            GUILayout.Space(2);
            if (GUILayout.Button("Delete Point"))
            {
                data.points.Remove(contextPoint);
                var toRemove = paths.Keys.Where(pp => pp.idA == contextPoint.id || pp.idB == contextPoint.id).ToList();
                foreach (var key in toRemove) paths.Remove(key);
                UpdateDataModelPaths();
                data.mainLoopNodeIds.RemoveAll(id => id == contextPoint.id);
                contextPoint = null;
                showContextMenu = false;
                Repaint();
                GUILayout.EndArea();
                return;
            }
        }
        else
        {
            GUILayout.Label("Add Special Point", EditorStyles.boldLabel);
            if (GUILayout.Button("Station"))
            {
                AddSpecialPoint(PointType.Station, contextMenuCell.x, contextMenuCell.y);
                showContextMenu = false;
            }
            if (GUILayout.Button("Junction"))
            {
                AddSpecialPoint(PointType.Junction, contextMenuCell.x, contextMenuCell.y);
                showContextMenu = false;
            }
            if (GUILayout.Button("End Station"))
            {
                AddSpecialPoint(PointType.EndStation, contextMenuCell.x, contextMenuCell.y);
                showContextMenu = false;
            }
        }
        GUILayout.EndArea();
    }

    private void AddSpecialPoint(PointType type, int col, int row)
    {
        foreach (var pt in data.points)
            if (pt.col == col && pt.row == row)
                return;

        data.points.Add(new SpecialPoint
        {
            id = data.nextPointId++,
            col = col,
            row = row,
            type = type
        });
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

    private void DrawHoverInfo(SpecialPoint pt, Vector2 mousePos)
    {
        string info = $"ID: {pt.id}\nType: {pt.type}\nCell: ({pt.col},{pt.row})";
        Vector2 size = EditorStyles.helpBox.CalcSize(new GUIContent(info));
        Rect rect = new Rect(mousePos.x + 16, mousePos.y - 4, size.x + 12, size.y + 12);

        EditorGUI.DrawRect(rect, Color.white);

        var hoverLabelStyle = new GUIStyle(EditorStyles.helpBox)
        {
            normal = { textColor = Color.black },
            fontSize = 10,
            alignment = TextAnchor.UpperLeft,
            wordWrap = true
        };

        GUI.Label(rect, info, hoverLabelStyle);
    }

    private void DrawAllPaths()
    {
        foreach (var kvp in paths)
        {
            if (selectedPathPair != null && kvp.Key.Equals(selectedPathPair))
                continue;

            DrawPathCells(kvp.Value, Color.yellow, 6f);
        }

        if (selectedPathPair != null && paths.TryGetValue(selectedPathPair, out var selPath))
        {
            DrawPathCells(selPath, new Color(0.1f, 0.6f, 1f, 1f), 3f);
        }
    }

    private void DrawMainLoop()
    {
        if (data.mainLoopNodeIds.Count < 2) return;
        List<SpecialPoint> loopNodes = data.mainLoopNodeIds
            .Select(id => data.points.FirstOrDefault(p => p.id == id))
            .Where(p => p != null)
            .ToList();

        Handles.color = new Color(1f, 0.5f, 0.15f, 1f);
        for (int i = 0; i < loopNodes.Count - 1; i++)
        {
            Vector2 from = GetCellCenter(loopNodes[i].col, loopNodes[i].row);
            Vector2 to = GetCellCenter(loopNodes[i + 1].col, loopNodes[i + 1].row);
            Handles.DrawAAPolyLine(8f, from, to);
        }
        if (loopNodes.Count > 2 && loopNodes[0].id == loopNodes[loopNodes.Count - 1].id)
        {
            Vector2 from = GetCellCenter(loopNodes[loopNodes.Count - 2].col, loopNodes[loopNodes.Count - 2].row);
            Vector2 to = GetCellCenter(loopNodes[0].col, loopNodes[0].row);
            Handles.DrawAAPolyLine(8f, from, to);
        }
        foreach (var node in loopNodes)
        {
            Vector2 center = GetCellCenter(node.col, node.row);
            Handles.DrawSolidDisc(center, Vector3.forward, 3.5f * gridZoom);
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
        UpdateDataModelPaths();
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(path, json);
        Debug.Log($"Track grid saved to: {path}");
    }

    private void LoadFromJson(string path)
    {
        string json = File.ReadAllText(path);
        data = JsonUtility.FromJson<GridTrackDataModel>(json);
        RefreshRuntimePaths();
        selectedPathPair = null;
    }

    private void RefreshRuntimePaths()
    {
        paths.Clear();
        if (data == null) data = new GridTrackDataModel();
        foreach (var pd in data.paths)
        {
            var pair = new PathPair(pd.idA, pd.idB);
            paths[pair] = pd.cells != null ? new List<Vector2Int>(pd.cells) : new List<Vector2Int>();
        }
    }

    private void UpdateDataModelPaths()
    {
        data.paths.Clear();
        foreach (var kvp in paths)
        {
            data.paths.Add(new PathData
            {
                idA = kvp.Key.idA,
                idB = kvp.Key.idB,
                cells = new List<Vector2Int>(kvp.Value)
            });
        }
    }

    private void ClearAllPaths()
    {
        paths.Clear();
        data.paths.Clear();
        selectedPathPair = null;
        Repaint();
    }
}