using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine.Rendering;
using System.Security.Cryptography;


public class TrackLevelEditorWindow : EditorWindow
{
    private enum EditMode { Track, Game }
    private EditMode currentMode = EditMode.Track;

    private List<TrackPart> partsLibrary = new List<TrackPart>();
    private int selectedPartIndex = 0;

    private const int gridWidth = 30;
    private const int gridHeight = 50;

    private const int cellSize = 15; // Use this everywhere

    private Vector2 gridScroll;

    private PlacedPartInstance draggedPart = null;
    private Vector2 dragOffset;
    private bool isDragging = false;
    private Rect gridRect;

    Vector2 gridOrigin = new Vector2(0, 0);

    LevelData levelData = new LevelData();
    GameEditor gameEditor;

    private PathTrackGraph trackGraph; // assume initialized
    private PathFinder pathFinder;
    private PathVisualizer pathVisualizer;

    private int partCounter = 1;

    CellOccupationManager cellManager;

    private int fromIdx = 0;
    private int toIdx = 1;

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
        // GameEditor only needs gameData now
        gameEditor = new GameEditor(levelData.gameData);

        partsLibrary.Clear();
        TextAsset jsonText = Resources.Load<TextAsset>("parts");
        if (jsonText != null)
        {
            partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(jsonText.text);

            cellManager = new CellOccupationManager(partsLibrary);
        }
        else
        {
            Debug.LogError("Could not find parts.json in Resources.");
        }

        //BuildTrackGraph(); // Always (re)initialize on open

        pathFinder = new PathFinder(trackGraph);
        pathVisualizer = new PathVisualizer();

    }

    private void OnGUI()
    {
        DrawPartPicker();
        GUILayout.Space(8);

        // Handle mouse wheel for zoom in grid area
        gridRect = new Rect(0, 0, gridWidth * cellSize + 16, gridHeight * cellSize + 16);

        DrawGrid();

        // Determine which cell was clicked (if any), and handle based on currentMode
        if (Event.current.type == EventType.MouseDown && gridRect.Contains(Event.current.mousePosition))
        {
            // Convert mouse position to grid cell coordinates
            Vector2 localMouse = (Event.current.mousePosition - gridOrigin);
            int gx = Mathf.FloorToInt((Event.current.mousePosition.x - gridRect.x) / cellSize);
            int gy = Mathf.FloorToInt((Event.current.mousePosition.y - gridRect.y) / cellSize);

            // Check bounds
            if (gx >= 0 && gx < gridWidth && gy >= 0 && gy < gridHeight)
            {
                if (currentMode == EditMode.Track)
                {
                    HandleGridMouse(gridRect);
                }
                // Find the PlacedPartInstance for the clicked cell (gx, gy)
                PlacedPartInstance clickedPart = null;
                Vector2Int clickedCell = new Vector2Int(gx, gy);

                foreach (var partInstance in levelData.parts)
                {
                    var occupiedCells = GetOccupiedCells(partInstance);
                    if (occupiedCells.Contains(clickedCell))
                    {
                        clickedPart = partInstance;
                        break; // Found the part, exit the loop
                    }
                }

                // You can now use 'clickedPart' for further logic
                // For example, you could pass it to the station placement or store association

                // Proceed with the original call
                gameEditor.OnGridCellClicked(clickedPart,gx, gy, Event.current.button, GamePointType.Station, 0);

                // Optional: Debug log
                if (clickedPart != null)
                {
                    Debug.Log($"Station placed on part: {clickedPart.partType} at {clickedPart.position}");
                }
                else
                {
                    Debug.LogWarning($"No PlacedPartInstance found for cell ({gx},{gy})");
                }

                Event.current.Use();
                Repaint();
            }
        }

        if (currentMode == EditMode.Game)
        {
            DrawGamePoints();
            DrawStationsUI(gridRect, gameEditor.GetPoints());
        }

        GUILayout.Space(8);

        DrawSaveLoadButtons();

        if (GUILayout.Button("Clear Grid"))
        {
            ClearGrid();
            Repaint(); // If needed, to update the editor display
        }

        string[] modeNames = { "Track Editing", "Game Editing" };
        currentMode = (EditMode)GUILayout.SelectionGrid((int)currentMode, modeNames, modeNames.Length);


        // Get stations from GameData
        var stations = levelData.gameData.points;
        if (stations != null && stations.Count >= 2)
        {
            // Display dropdowns for start/end station
            string[] stationNames = new string[stations.Count];
            for (int i = 0; i < stations.Count; i++)
            {
                stationNames[i] = $"Station {i} ({stations[i].gridX}, {stations[i].gridY})";
            }

            // Store dropdown selection indexes in the editor class
            if (fromIdx >= stations.Count) fromIdx = 0;
            if (toIdx >= stations.Count) toIdx = 1;

            fromIdx = EditorGUILayout.Popup("From station", fromIdx, stationNames);
            toIdx = EditorGUILayout.Popup("To station", toIdx, stationNames);

            EditorGUI.BeginDisabledGroup(fromIdx == toIdx);
            if (GUILayout.Button("Show Path"))
            {
                var fromStation = stations[fromIdx];
                var toStation = stations[toIdx];

                var startPart = fromStation.part;
                var endPart = toStation.part;

                // If you want pathfinder to pick any exit, you can pass -1 or 0, depending on your implementation.
                // Or, update your FindPath method to allow "any exit" at the destination.

                int startExitIdx = 0; // Or whichever exit you want to start from
                int endExitIdx = 0;   // Or whichever exit you want to reach at the destination

                if (startPart != null && endPart != null)
                {
                    var path = pathFinder.FindPath(startPart, startExitIdx, endPart, endExitIdx);
                    // Draw path in your editor window/grid here
                    DrawPathPreview(stations, path);
                }
                else
                {
                    Debug.LogWarning("Cannot find start/end station on track.");
                }
            }
            EditorGUI.EndDisabledGroup();
        }
        else
        {
            EditorGUILayout.HelpBox("You need at least two stations (points) in GameData.", MessageType.Warning);
        }
    }


        private void DrawGamePoints()
    {
        foreach (var point in gameEditor.GetPoints())
        {
            Vector2 cellCenter = new Vector2(
                gridRect.x + point.gridX * cellSize + cellSize / 2,
                gridRect.y + point.gridY * cellSize + cellSize / 2
            );

            Handles.color = colors[point.colorIndex % colors.Length];
            float radius = 10f;
            Handles.DrawSolidDisc(cellCenter, Vector3.forward, radius);

            // Draw the type letter
            string letter = point.Letter; // gets "S", "D", or "T"

            letter += "_" + point.id;

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            Vector2 labelSize = style.CalcSize(new GUIContent(letter));
            Rect labelRect = new Rect(
                cellCenter.x - labelSize.x / 2,
                cellCenter.y - labelSize.y / 2,
                labelSize.x,
                labelSize.y
            );
            GUI.Label(labelRect, letter, style);
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

        gridRect = GUILayoutUtility.GetRect(gridWidth * cellSize + 16, gridHeight * cellSize + 16, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
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

        // Precompute connection positions (rotated)
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

        // === MODIFIED SECTION: draw splines ===
        Handles.color = Color.magenta;

        var splines = part.GetSplinesAsVector2();
        for (int i = 0; i < splines.Count; i++)
        {
            var spline = splines[i];
            // Convert spline local [0,w],[0,h] to screen (rotated)
            Vector3[] pts = new Vector3[spline.Count];
            for (int j = 0; j < spline.Count; j++)
            {
                Vector2 pt = spline[j];
                float gx = pt.x * cellSize;
                float gy = pt.y * cellSize;
                Vector2 gridPt = new Vector2(partRect.x + gx, partRect.y + gy);
                Vector2 partCenter = new Vector2(partRect.x + partRect.width / 2f, partRect.y + partRect.height / 2f);
                Vector2 rotatedPt = RotatePointAround(gridPt, partCenter, placed.rotation);
                pts[j] = rotatedPt;
            }
            Handles.DrawAAPolyLine(4f, pts);

            // Draw spline endpoints as discs (optional)
            Handles.DrawSolidDisc(pts.First(), Vector3.forward, 4f);
            Handles.DrawSolidDisc(pts.Last(), Vector3.forward, 4f);
        }

        // === END MODIFIED SECTION ===

        // Optionally: draw connection discs (yellow)
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

        if (currentMode == EditMode.Track)
        {
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
                        PrintOccupiedCells(clickedPart, clickedTrackPart); // Print after rotate

                        cellManager.AddOrUpdatePart(clickedPart);

                        e.Use();
                        Repaint();
                    }
                    else if (e.button == 2) // Middle: delete
                    {
                        levelData.parts.Remove(clickedPart);

                        cellManager.RemovePart(clickedPart);

                        e.Use();
                        Repaint();
                    }
                }
                else if (e.button == 0) // Left: place new part
                {
                    TrackPart selectedPart = partsLibrary[selectedPartIndex];
                    int placeWidth = selectedPart.gridWidth;
                    int placeHeight = selectedPart.gridHeight;

                    var newInstance = new PlacedPartInstance
                    {
                        partType = selectedPart.partName,         // Reference the part name from TrackPart
                        partId = GenerateUniquePartId(selectedPart.partName),
                        position = new Vector2Int(gx, gy),
                        rotation = 0,
                        splines = new List<List<Vector2>>()
                    };
                    levelData.parts.Add(newInstance);

                    PrintOccupiedCells(newInstance, selectedPart); // Print after placement

                    cellManager.AddOrUpdatePart(newInstance);

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
                    PrintOccupiedCells(draggedPart, partsLibrary.Find(p => p.partName == draggedPart.partType)); // Print after move

                    cellManager.AddOrUpdatePart(draggedPart);

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
        else if (currentMode == EditMode.Game)
        {
            if (Event.current.type == EventType.MouseDown)
            {
                string btn = Event.current.button switch
                {
                    0 => "Left",
                    1 => "Right",
                    2 => "Middle",
                    _ => $"Button {Event.current.button}"
                };
                //Debug.Log($"Game Edit: Clicked grid cell ({gx}, {gy}) with {btn} mouse button.");
            }
        }
    }

    // Add this helper method to your class
    private void PrintOccupiedCells(PlacedPartInstance instance, TrackPart model)
    {
        return;

        int width = (instance.rotation % 180 == 0) ? model.gridWidth : model.gridHeight;
        int height = (instance.rotation % 180 == 0) ? model.gridHeight : model.gridWidth;
        Debug.Log($"Part '{instance.partType}' (ID {instance.partId}) at {instance.position} with rotation {instance.rotation} occupies:");

        for (int dx = 0; dx < width; dx++)
        {
            for (int dy = 0; dy < height; dy++)
            {
                Vector2Int cell = new Vector2Int(instance.position.x + dx, instance.position.y + dy);
                Debug.Log($"  Cell: {cell}");
            }
        }
    }

    private string GenerateUniquePartId(string partName)
    {
        return $"{partName}_{partCounter++}";
    }

    private void DrawSaveLoadButtons()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Level"))
        {
            string path = EditorUtility.SaveFilePanel("Save Level JSON", Application.dataPath, "level.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                if (levelData.gameData == null)
                    levelData.gameData = new GameModel();

                levelData.gameData.points = gameEditor.GetPoints();

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

                // ADD THIS LINE:
                gameEditor.SetPoints(levelData.gameData.points);

                // When editor loads a level:
                cellManager = new CellOccupationManager(partsLibrary); // Create new cell manager
                cellManager.BuildFromLevel(levelData.parts);           // Fill dictionary from all placed parts

                OnLoadLevel(levelData.parts);

                SplineHelper.CopySplinesToPlacedParts(levelData.parts, partsLibrary);

                List<PathPartConnection> connections = PathTrackGraphBuilder.BuildConnectionsFromGrid(levelData.parts, partsLibrary);

                trackGraph = PathTrackGraphBuilder.BuildPathTrackGraph(levelData.parts,connections);

                pathFinder = new PathFinder(trackGraph);

                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    //ensures i can edit loaded levels
    public void OnLoadLevel(List<PlacedPartInstance> loadedParts)
    {
        int maxIndex = 0;
        foreach (var part in loadedParts)
        {
            // IDs are like "NameAAA_12"
            string[] tokens = part.partId.Split('_');
            if (tokens.Length > 1 && int.TryParse(tokens[tokens.Length - 1], out int idx))
            {
                if (idx > maxIndex)
                    maxIndex = idx;
            }
        }
        partCounter = maxIndex + 1; // Next generated part will get a unique ID
    }


    public void ClearGrid()
    {
        if (levelData != null && levelData.parts != null)
        {
            levelData.parts.Clear();
        }

        cellManager.cellToPart.Clear(); // Remove all cell occupation mappings
    }

    private readonly Color[] colors = new Color[]
    {
        new Color(0.2f, 0.3f, 0.6f),  // Dark Blue
        new Color(0.1f, 0.5f, 0.2f),  // Dark Green
        new Color(0.5f, 0.15f, 0.15f) // Dark Red
    };

    private void DrawStationsUI(Rect gridRect, List<GamePoint> points)
    {
        float stationWidth = 160f;
        float stationHeight = 60f;
        float personSize = 24f;
        float spacing = 15f;

        // Find station points
        var stations = points.Where(p => p.type == GamePointType.Station).ToList();

        for (int i = 0; i < stations.Count; i++)
        {
            var station = stations[i];
            Rect stationRect = new Rect(gridRect.xMax + spacing, gridRect.y + i * (stationHeight + spacing), stationWidth, stationHeight);

            // Get cell and part info
            Vector2Int cell = new Vector2Int(station.gridX,station.gridY);
            string partId = "none";
            if (cellManager != null && cellManager.cellToPart.TryGetValue(cell, out PlacedPartInstance part))
            {
                partId = part.partId;
            }

            // Draw station label and cell/part info
            GUI.Label(new Rect(stationRect.x, stationRect.y, stationRect.width*5, 20f),
                      $"Station {station.id} | Cell {cell} | Part: {partId}",
                      new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold });

            // Draw waiting people
            for (int j = 0; j < station.waitingPeople.Count; j++)
            {
                int colorIdx = station.waitingPeople[j];
                Rect personRect = new Rect(stationRect.x + j * (personSize + 5f), stationRect.y + 24f, personSize, personSize);

                EditorGUI.DrawRect(personRect, colors[colorIdx % colors.Length]);
                // Draw border
                Handles.color = Color.black;
                Handles.DrawSolidRectangleWithOutline(personRect, Color.clear, Color.black);

                // Handle click to cycle color
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                    personRect.Contains(Event.current.mousePosition))
                {
                    station.waitingPeople[j] = (colorIdx + 1) % colors.Length;
                    Event.current.Use();
                    Repaint();
                }
            }

            // Button to add a person
            Rect addBtnRect = new Rect(stationRect.x, stationRect.y + stationHeight - 8f, 80f, 24f);
            if (GUI.Button(addBtnRect, "Add Person"))
            {
                station.waitingPeople.Add(0); // Add person with color index 0
                Repaint();
            }
        }
    }


    public List<Vector2Int> GetOccupiedCells(PlacedPartInstance instance)
    {
        // Find the TrackPart model by name using LINQ's FirstOrDefault
        TrackPart model = partsLibrary.FirstOrDefault(part => part.partName == instance.partType);
        if (model == null)
        {
            UnityEngine.Debug.LogWarning($"TrackPart not found for name: {instance.partType}");
            return new List<Vector2Int>();
        }

        var cells = new List<Vector2Int>();

        for (int dx = 0; dx < model.gridWidth; dx++)
        {
            for (int dy = 0; dy < model.gridHeight; dy++)
            {
                Vector2Int local = new Vector2Int(dx, dy);
                Vector2Int rotated = RotateOffset(local, instance.rotation, model.gridWidth, model.gridHeight);
                Vector2Int cell = instance.position + rotated;
                cells.Add(cell);
            }
        }
        return cells;
    }

    // Rotates an offset according to part rotation (anchor at top-left)
    public static Vector2Int RotateOffset(Vector2Int offset, int rotation, int width, int height)
    {
        switch (rotation % 360)
        {
            case 0: return offset;
            case 90: return new Vector2Int(height - 1 - offset.y, offset.x);
            case 180: return new Vector2Int(width - 1 - offset.x, height - 1 - offset.y);
            case 270: return new Vector2Int(offset.y, width - 1 - offset.x);
            default:
                UnityEngine.Debug.LogWarning("Unexpected rotation value");
                return offset;
        }
    }


    void DrawPathPreview(List<GamePoint> stations, List<PathSegment> path)
    {
        // Use gridSize and gridMargin from your editor class
        // Compute bounds
        int minX = stations.Min(s => s.gridX);
        int minY = stations.Min(s => s.gridY);
        int maxX = stations.Max(s => s.gridX);
        int maxY = stations.Max(s => s.gridY);

        int gridW = (maxX - minX + 1) * cellSize;
        int gridH = (maxY - minY + 1) * cellSize;

        Rect gridRect = GUILayoutUtility.GetRect(gridW  * 2, gridH  * 2);

        // Draw grid
        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                Rect cellRect = new Rect(
                    gridRect.x + (x - minX) * cellSize,
                    gridRect.y + (y - minY) * cellSize,
                    cellSize, cellSize);
                EditorGUI.DrawRect(cellRect, new Color(0.9f, 0.9f, 0.9f, 1));
            }

        // Draw stations
        foreach (var s in stations)
        {
            Rect cellRect = new Rect(
                gridRect.x + (s.gridX - minX) * cellSize,
                gridRect.y + (s.gridY - minY) * cellSize,
                cellSize, cellSize);
            EditorGUI.DrawRect(cellRect, Color.yellow);
            GUI.Label(cellRect, $"S");
        }

        // Draw path (as red lines)
        if (path != null && path.Count > 0)
        {
            Handles.BeginGUI();
            Handles.color = Color.red;
            for (int i = 0; i < path.Count; i++)
            {
                var seg = path[i];
                if (seg.part == null) continue;
                Vector2 startPos = seg.part.GetPositionOnSpline(seg.tStart);
                Vector2 endPos = seg.part.GetPositionOnSpline(seg.tEnd);

                // Convert world positions to grid positions
                Vector2 guiStart = gridRect.position + new Vector2(
                    (startPos.x - minX) * cellSize + cellSize / 2,
                    (startPos.y - minY) * cellSize + cellSize / 2);
                Vector2 guiEnd = gridRect.position + new Vector2(
                    (endPos.x - minX) * cellSize + cellSize / 2,
                    (endPos.y - minY) * cellSize + cellSize / 2);

                Handles.DrawLine(guiStart, guiEnd, 2f);
            }
            Handles.EndGUI();
        }
    }
}