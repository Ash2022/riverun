using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;


public class TrackLevelEditorWindow : EditorWindow
{
    private List<TrackPart> partsLibrary = new List<TrackPart>();
    private int selectedPartIndex = 0;

    private const int gridWidth = 30;
    private const int gridHeight = 50;

    private const int baseCellSize = 20;
    public int cellSize => Mathf.RoundToInt(baseCellSize * gridZoom);

    private Vector2 gridScroll;

    private PlacedPartInstance draggedPart = null;
    private Vector2 dragOffset;
    private bool isDragging = false;

    private float gridZoom = 1f;
    private const float minZoom = 0.5f;
    private const float maxZoom = 2f;

    LevelData levelData = new LevelData();
    

    [MenuItem("Tools/Track Level Editor")]
    public static void ShowWindow()
    {
        GetWindow<TrackLevelEditorWindow>("Track Level Editor");
    }

    private void OnEnable()
    {
        LoadPartsLibrary();
    }

    private void LoadPartsLibrary()
    {
        levelData.parts = new List<PlacedPartInstance>();
        partsLibrary.Clear();
        TextAsset jsonText = Resources.Load<TextAsset>("parts");
        if (jsonText != null)
        {
            partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(jsonText.text);
        }
        else
        {
            Debug.LogError("Could not find parts.json in Resources.");
        }
    }

    private void OnGUI()
    {
        DrawPartPicker();
        GUILayout.Space(8);

        // Handle mouse wheel for zoom in grid area
        Rect gridRect = new Rect(0, 0, gridWidth * cellSize + 16, gridHeight * cellSize + 16);
        if (Event.current.type == EventType.ScrollWheel && gridRect.Contains(Event.current.mousePosition))
        {
            float zoomDelta = -Event.current.delta.y * 0.05f;
            gridZoom = Mathf.Clamp(gridZoom + zoomDelta, minZoom, maxZoom);
            Event.current.Use();
            Repaint();
        }

        DrawGrid();

        GUILayout.Space(8);

        DrawSaveLoadButtons();

        if (GUILayout.Button("Clear Grid"))
        {
            ClearGrid();
            Repaint(); // If needed, to update the editor display
        }
    }

    private void DrawPartPicker()
    {
        GUILayout.Label("Pick a Part", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        for (int i = 0; i < partsLibrary.Count; i++)
        {
            TrackPart part = partsLibrary[i];
            Texture2D img = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(part.displaySprite));
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fixedWidth = 36;
            style.fixedHeight = 36;
            if (GUILayout.Toggle(selectedPartIndex == i, new GUIContent(img), style))
                selectedPartIndex = i;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGrid()
    {
        GUILayout.Label("Level Grid", EditorStyles.boldLabel);

        Rect gridRect = GUILayoutUtility.GetRect(gridWidth * cellSize + 16, gridHeight * cellSize + 16, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        GUI.Box(gridRect, "");

        // Draw cells
        Handles.BeginGUI();
        Handles.color = Color.gray;
        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = gridRect.x + x * cellSize;
            Handles.DrawLine(new Vector3(xPos, gridRect.y), new Vector3(xPos, gridRect.y + gridHeight * cellSize));
        }
        for (int y = 0; y <= gridHeight; y++)
        {
            float yPos = gridRect.y + y * cellSize;
            Handles.DrawLine(new Vector3(gridRect.x, yPos), new Vector3(gridRect.x + gridWidth * cellSize, yPos));
        }

        // Draw placed parts
        for (int i = 0; i < levelData.parts.Count; i++)
        {
            var placed = levelData.parts[i];
            TrackPart part = partsLibrary.Find(p => p.partName == placed.partType);
            if (part == null) continue;

            float px = gridRect.x + placed.position.x * cellSize;
            float py = gridRect.y + placed.position.y * cellSize;
            float pw = part.gridWidth * cellSize;
            float ph = part.gridHeight * cellSize;

            int w = part.gridWidth;
            int h = part.gridHeight;
            int rotation = placed.rotation % 360;

            float offsetX = 0f, offsetY = 0f;
            float offsetX2 = 0f, offsetY2 = 0f;

            if(w !=h)//3*2
            {
                if (rotation == 90)
                {
                    offsetX = (h - w) / -2f * cellSize;
                    offsetY = (w - h) / -2f * cellSize;

                    offsetX2 = (h-w)*cellSize / 2;
                    offsetY2 = (h-w)*cellSize / 2;
                }

                if (rotation == 270)
                {
                    offsetX = (h - w) / 2f * cellSize;
                    offsetY = (w - h) / 2f * cellSize;

                    offsetX2 = (h - w) * cellSize / 2;
                    offsetY2 = (h - w) * cellSize / 2;
                }
            }
           

            Vector2 pivot = new Vector2(px + pw / 2f, py + ph / 2f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(placed.rotation, pivot);
            Texture2D img = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(part.displaySprite));
            if (img != null)
                GUI.DrawTexture(new Rect(px+offsetX, py-offsetY, pw, ph), img, ScaleMode.ScaleToFit);
            GUI.matrix = oldMatrix;

            DrawSplinesForPart(part, placed, new Rect(px+offsetX2, py-offsetY2, pw, ph));
        }
        Handles.EndGUI();

        // Handle mouse events for placing, rotating, deleting
        HandleGridMouse(gridRect);
    }

    private void DrawSplinesForPart(TrackPart part, PlacedPartInstance placed, Rect partRect)
    {
        int rotCount = placed.rotation / 90;

        var connPos = new Dictionary<int, Vector2>();

        foreach (var c in part.connections)
        {
            Vector2 pos;

            if (part.gridWidth == 1 && part.gridHeight == 1)
            {
                pos = GetConnectionPosition1x1(partRect, c.direction, rotCount);
            }
            else
            {
                float cellCenterX = partRect.x + (c.gridOffset[0] + 0.5f) * cellSize;
                float cellCenterY = partRect.y + (c.gridOffset[1] + 0.5f) * cellSize;

                float offsetX = 0, offsetY = 0;
                switch (c.direction)
                {
                    case 0: offsetY = -cellSize / 2f; break; // Up
                    case 1: offsetX = cellSize / 2f; break;  // Right
                    case 2: offsetY = cellSize / 2f; break;  // Down
                    case 3: offsetX = -cellSize / 2f; break; // Left
                }
                Vector2 cellPos = new Vector2(cellCenterX + offsetX, cellCenterY + offsetY);

                Vector2 partCenter = new Vector2(partRect.x + partRect.width / 2f, partRect.y + partRect.height / 2f);
                pos = RotatePointAround(cellPos, partCenter, placed.rotation);
            }
            connPos[c.id] = pos;
        }

        Handles.color = Color.cyan;
        foreach (var path in part.allowedPaths)
        {
            Vector2 from = connPos[path.entryConnectionId];
            Vector2 to = connPos[path.exitConnectionId];
            Handles.DrawAAPolyLine(3f, from, to);
            Handles.DrawSolidDisc(from, Vector3.forward, 3f);
            Handles.DrawSolidDisc(to, Vector3.forward, 3f);
        }

        Handles.color = Color.yellow;
        foreach (var kvp in connPos)
        {
            Handles.DrawSolidDisc(kvp.Value, Vector3.forward, 2f);
        }
    }

    private Vector2 GetConnectionPosition1x1(Rect previewRect, int direction, int rotation)
    {
        int dir = (direction + rotation) % 4;
        float cx = previewRect.x + previewRect.width / 2f;
        float cy = previewRect.y + previewRect.height / 2f;
        switch (dir)
        {
            case 0: return new Vector2(cx, previewRect.y); // Up
            case 1: return new Vector2(previewRect.x + previewRect.width, cy); // Right
            case 2: return new Vector2(cx, previewRect.y + previewRect.height); // Down
            case 3: return new Vector2(previewRect.x, cy); // Left
        }
        return new Vector2(cx, cy);
    }

    private static Vector2 RotatePointAround(Vector2 pt, Vector2 pivot, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cosA = Mathf.Cos(rad);
        float sinA = Mathf.Sin(rad);
        Vector2 d = pt - pivot;
        return pivot + new Vector2(
            cosA * d.x - sinA * d.y,
            sinA * d.x + cosA * d.y
        );
    }

    private void HandleGridMouse(Rect gridRect)
    {
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;
        if (!gridRect.Contains(mousePos)) return;

        int gx = Mathf.FloorToInt((mousePos.x - gridRect.x) / cellSize);
        int gy = Mathf.FloorToInt((mousePos.y - gridRect.y) / cellSize);

        // Find clicked part
        PlacedPartInstance clickedPart = null;
        TrackPart clickedTrackPart = null;
        for (int i = levelData.parts.Count - 1; i >= 0; i--)
        {
            var placed = levelData.parts[i];
            TrackPart part = partsLibrary.Find(p => p.partName == placed.partType);
            if (part == null) continue;
            int partWidth = (placed.rotation % 180 == 0) ? part.gridWidth : part.gridHeight;
            int partHeight = (placed.rotation % 180 == 0) ? part.gridHeight : part.gridWidth;
            if (gx >= placed.position.x && gx < placed.position.x + partWidth &&
                gy >= placed.position.y && gy < placed.position.y + partHeight)
            {
                clickedPart = placed;
                clickedTrackPart = part;
                break;
            }
        }

        // Mouse button logic
        if (e.type == EventType.MouseDown)
        {
            if (clickedPart != null)
            {
                if (e.button == 0) // Left: start drag
                {
                    draggedPart = clickedPart;
                    dragOffset = new Vector2(gx - draggedPart.position.x, gy - draggedPart.position.y);
                    isDragging = true;
                    e.Use();
                }
                else if (e.button == 1) // Right: rotate
                {
                    clickedPart.rotation = (clickedPart.rotation + 90) % 360;
                    e.Use();
                    Repaint();
                }
                else if (e.button == 2) // Middle: delete
                {
                    levelData.parts.Remove(clickedPart);
                    e.Use();
                    Repaint();
                }
            }
            else if (e.button == 0) // Left: place new part
            {
                TrackPart selectedPart = partsLibrary[selectedPartIndex];
                int placeWidth = selectedPart.gridWidth;
                int placeHeight = selectedPart.gridHeight;

                // Remove overlap check (allow overlapping)
                levelData.parts.Add(new PlacedPartInstance
                {
                    partType = selectedPart.partName,         // Reference the part name from TrackPart
                    partId = GenerateUniquePartId(),          // If you want to assign a unique ID, implement GenerateUniquePartId()
                    position = new Vector2Int(gx, gy),        // Use Vector2Int for grid position
                    rotation = 0,                             // Rotation in degrees
                    splines = new List<List<Vector2>>()       // Initialize splines as empty or with default values
                });
                e.Use();
                Repaint();
            }
        }

        // Dragging part
        if (isDragging && draggedPart != null)
        {
            if (e.type == EventType.MouseDrag)
            {
                draggedPart.position.x = gx - (int)dragOffset.x;
                draggedPart.position.y = gy - (int)dragOffset.y;
                e.Use();
                Repaint();
            }
            else if (e.type == EventType.MouseUp)
            {
                isDragging = false;
                draggedPart = null;
                e.Use();
                Repaint();
            }
        }
    }

    private int GenerateUniquePartId()
    {
        return 0;
    }

    private void DrawSaveLoadButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Level"))
        {
            string path = EditorUtility.SaveFilePanel("Save Level JSON", Application.dataPath, "level.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(levelData, Formatting.Indented));
                AssetDatabase.Refresh();
            }
        }
        if (GUILayout.Button("Load Level"))
        {
            string path = EditorUtility.OpenFilePanel("Load Level JSON", Application.dataPath, "json");
            if (!string.IsNullOrEmpty(path))
            {
                string json = File.ReadAllText(path);
                
                levelData = JsonConvert.DeserializeObject<LevelData>(json);


                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    public void ClearGrid()
    {
        if (levelData != null && levelData.parts != null)
        {
            levelData.parts.Clear();
        }
    }
}