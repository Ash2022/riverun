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
using static PlacedPartInstance;


public class TrackLevelEditorWindow : EditorWindow
{
    private enum EditMode { Track, Game }
    private EditMode currentMode = EditMode.Track;

    private List<TrackPart> partsLibrary = new List<TrackPart>();
    private int selectedPartIndex = 0;

    private const int gridWidth = 7;//30;
    private const int gridHeight = 9;//50;

    private const int cellSize = 50;//15; // Use this everywhere

    private Vector2 gridScroll;

    private PlacedPartInstance draggedPart = null;
    private Vector2 dragOffset;
    private bool isDragging = false;
    private Rect gridRect;

    Vector2 gridOrigin = new Vector2(0, 0);

    LevelData levelData = new LevelData();
    GameEditor gameEditor;

    
    private PathVisualizer pathVisualizer;
    
    GraphBuilder graphBuilder;
    GraphModel gameGraph;
    PathFinder pathFinder;
    PathModel currPath;
    
    //List<PathStep> currPath;

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

        
        graphBuilder = new GraphBuilder();
        pathVisualizer = new PathVisualizer();
        pathFinder = new PathFinder();
        
    }

    private void OnGUI()
    {

        //Debug.Log($"OnGUI event: {Event.current.type}, frame: {Time.frameCount}");

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
                    var occupiedCells = GetOccupiedCells(partInstance,partsLibrary);
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

            
            if (currPath != null)
            {
                DrawPath(currPath);
            }
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

            if (GUILayout.Button("BuildGraph"))
            {
                BuildGameGraph();
            }

            if (GUILayout.Button("Show Path"))
            {
                var fromStation = stations[fromIdx];
                var toStation = stations[toIdx];

                var startPart = fromStation.part;
                var endPart = toStation.part;

                // If you want pathfinder to pick any exit, you can pass -1 or 0, depending on your implementation.
                // Or, update your FindPath method to allow "any exit" at the destination.

                if (startPart != null && endPart != null)
                {
                    currPath = pathFinder.GetPath(startPart, endPart);
                    
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

            if (w != h)
            {
                if (rotation == 90)
                {
                    offsetX = (h - w) / -2f * cellSize;
                    offsetY = (w - h) / -2f * cellSize;
                }
                else if (rotation == 270)
                {
                    offsetX = (h - w) / 2f * cellSize;
                    offsetY = (w - h) / 2f * cellSize;
                }
            }

            Vector2 pivot = new Vector2(px + pw / 2f, py + ph / 2f);
            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(placed.rotation, pivot);

            // Apply offsets only to texture rendering
            Texture2D img = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(part.displaySprite));
            if (img != null)
                GUI.DrawTexture(new Rect(px + offsetX, py - offsetY, pw, ph), img, ScaleMode.ScaleToFit);
            GUI.matrix = oldMatrix;

            // Draw splines without offsets to ensure consistency for pathfinding
            DrawSplinesForPart(part, placed, new Rect(px, py, pw, ph));
        }
        Handles.EndGUI();

        // Handle mouse events for placing, rotating, deleting
        HandleGridMouse(gridRect);
    }

    private List<BakedSpline> DrawSplinesForPart(TrackPart part,
                                             PlacedPartInstance placed,
                                             Rect partRect,
                                             bool buildOnly = false)
    {
        float offsetX2 = 0f, offsetY2 = 0f;
        if (part.gridWidth != part.gridHeight)
        {
            int rot = placed.rotation % 360;
            if (rot == 90)
            {
                offsetX2 = (part.gridHeight - part.gridWidth) * cellSize / -2f;
                offsetY2 = (part.gridWidth - part.gridHeight) * cellSize / 2f;
            }
            else if (rot == 270)
            {
                offsetX2 = (part.gridHeight - part.gridWidth) * cellSize / 2f;
                offsetY2 = (part.gridHeight - part.gridWidth) * cellSize / 2f;
            }
        }

        var results = new List<BakedSpline>();
        var splines = part.GetSplinesAsVector2();

        for (int i = 0; i < splines.Count; i++)
        {
            var spline = splines[i];
            Vector3[] pts = new Vector3[spline.Count];

            var guiPts = new List<Vector2>(spline.Count);
            var gridPts = new List<Vector2>(spline.Count);

            for (int j = 0; j < spline.Count; j++)
            {
                Vector2 pt = spline[j];
                float gx = pt.x * cellSize;
                float gy = pt.y * cellSize;

                Vector2 gridPt = new Vector2(partRect.x + gx + offsetX2, partRect.y + gy + offsetY2);
                Vector2 partCent = new Vector2(partRect.x + partRect.width / 2f, partRect.y + partRect.height / 2f);

                Vector2 rotatedPt = RotatePointAround(gridPt, partCent, placed.rotation);
                pts[j] = rotatedPt;

                guiPts.Add(rotatedPt);                            // pixels for direct drawing later
                gridPts.Add(new Vector2(rotatedPt.x / cellSize,   // grid coords (already rotated)
                                        rotatedPt.y / cellSize));
            }

            results.Add(new BakedSpline { guiPts = guiPts, gridPts = gridPts });

            if (!buildOnly)
            {
                Handles.DrawAAPolyLine(4f, pts);
                Handles.DrawSolidDisc((Vector2)pts[0], Vector3.forward, 4f);
                Handles.DrawSolidDisc((Vector2)pts[pts.Length - 1], Vector3.forward, 4f);
            }
        }

        return results;
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
                        PopulatePlacedPartData(clickedPart, clickedTrackPart); // Print after rotate

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
                        allowedPathsGroup = selectedPart.allowedPathsGroups,
                        partType = selectedPart.partName,         // Reference the part name from TrackPart
                        partId = GenerateUniquePartId(selectedPart.partName),
                        position = new Vector2Int(gx, gy),
                        rotation = 0,
                        splines = new List<List<Vector2>>()
                    };
                    levelData.parts.Add(newInstance);

                    PopulatePlacedPartData(newInstance, selectedPart); // Print after placement

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
                    PopulatePlacedPartData(draggedPart, partsLibrary.Find(p => p.partName == draggedPart.partType)); // Print after move

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
    private void PopulatePlacedPartData(PlacedPartInstance instance, TrackPart model)
    {
        // Use the GetOccupiedCells method to retrieve all occupied cells
        List<Vector2Int> occupiedCells = GetOccupiedCells(instance, partsLibrary);

        Debug.Log($"--- Part Details ---");
        Debug.Log($"Name: {instance.partId}");
        Debug.Log($"Type: {instance.partType}");
        Debug.Log($"ID: {instance.partId}");
        Debug.Log($"Position: {instance.position}");
        Debug.Log($"Rotation: {instance.rotation} degrees");

        // Print the occupied cells
        Debug.Log($"Occupied Cells:");
        foreach (Vector2Int cell in occupiedCells)
        {
            Debug.Log($"  - Cell at {cell}");
        }

        // Initialize the exits list
        instance.exits = new List<PlacedPartInstance.ExitDetails>();

        // Populate the exits list and print the details
        Debug.Log($"Exits and Neighbor Cells:");
        foreach (var exit in model.connections)
        {
            // Calculate the rotated exit position and direction
            Vector2Int exitLocalCell = new Vector2Int(exit.gridOffset[0], exit.gridOffset[1]); // Local exit cell relative to the part
            Vector2Int rotatedExitCell = RotateGridPart(exitLocalCell, instance.rotation, model.gridWidth, model.gridHeight); // Rotated cell after applying rotation
            Vector2Int exitWorldCell = instance.position + rotatedExitCell; // World cell in the grid
            int rotatedExitDirection = (exit.direction + instance.rotation / 90) % 4; // Rotated direction after rotation

            // Calculate neighbor cell
            Vector2Int neighborCell = instance.position + rotatedExitCell + DirectionToOffset(rotatedExitDirection);

            // Create and add the exit details to the list
            PlacedPartInstance.ExitDetails exitDetails = new PlacedPartInstance.ExitDetails
            {
                exitIndex = exit.id,
                localCell = exitLocalCell,
                rotatedCell = rotatedExitCell,
                worldCell = exitWorldCell,
                direction = rotatedExitDirection,
                neighborCell = neighborCell
            };
            instance.exits.Add(exitDetails);
            instance.occupyingCells = occupiedCells;

            // Print exit details
            Debug.Log($"  - Exit Index: {exitDetails.exitIndex}");
            Debug.Log($"    Local Cell: {exitDetails.localCell}");
            Debug.Log($"    Rotated Cell: {exitDetails.rotatedCell}");
            Debug.Log($"    World Cell: {exitDetails.worldCell}");
            Debug.Log($"    Direction: {GetHumanReadableDirection(exitDetails.direction)}");
            Debug.Log($"    Neighbor Cell to Search: {exitDetails.neighborCell}");
        }

        

        // Update the spline values in grid coordinates

        float px = gridRect.x + instance.position.x * cellSize;
        float py = gridRect.y + instance.position.y * cellSize;
        float pw = model.gridWidth * cellSize;
        float ph = model.gridHeight * cellSize;

        Rect partRect = new Rect(px, py, pw, ph); // Assume CalculatePartRect gives the rectangle for the part
        instance.bakedSplines = DrawSplinesForPart(model, instance, partRect, true);

        // Print spline points
        Debug.Log($"Spline Points:");
        for (int i = 0; i < instance.splines.Count; i++)
        {
            Debug.Log($"  Spline {i}:");
            foreach (var point in instance.splines[i])
            {
                Debug.Log($"    - Point: {point}");
            }
        }

        // Map (entry,exit) -> spline index (group index, as you decided earlier)
        int FindSplineIndex(int entryId, int exitId)
        {
            if (instance.allowedPathsGroup == null) return 0;
            for (int g = 0; g < instance.allowedPathsGroup.Count; g++)
            {
                var grp = instance.allowedPathsGroup[g];
                if (grp.allowedPaths == null) continue;
                for (int a = 0; a < grp.allowedPaths.Count; a++)
                {
                    var ap = grp.allowedPaths[a];
                    if (ap.entryConnectionId == entryId && ap.exitConnectionId == exitId)
                        return g; // your convention: group index == spline index
                }
            }
            return 0;
        }

        // Fill length for each AllowedPath
        if (instance.allowedPathsGroup != null && instance.bakedSplines != null)
        {
            for (int g = 0; g < instance.allowedPathsGroup.Count; g++)
            {
                var grp = instance.allowedPathsGroup[g];
                if (grp.allowedPaths == null) continue;

                foreach (var ap in grp.allowedPaths)
                {
                    int splIdx = FindSplineIndex(ap.entryConnectionId, ap.exitConnectionId);

                    // Use gridPts to stay in grid units; multiply by cellSize later if needed
                    var poly = instance.bakedSplines[splIdx].gridPts;
                    ap.length = PolylineLength(poly);
                    // Debug
                    Debug.Log($"Path {instance.partId} {ap.entryConnectionId}->{ap.exitConnectionId} len={ap.length}");
                }
            }
        }


        Debug.Log($"--- End of Part Details ---");

        Debug.Log($"Spline values updated successfully in PlacedPartInstance.");
    }

    private float PolylineLength(List<Vector2> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++)
            len += Vector2.Distance(pts[i - 1], pts[i]);
        return len;
    }

    // Helper method to rotate a cell based on part rotation and grid dimensions
    private Vector2Int RotateGridPart(Vector2Int cell, int rotation, int partWidth, int partHeight)
    {
        // For 1x1 parts, no rotation is necessary (trivial case)
        if (partWidth == 1 && partHeight == 1)
        {
            return cell;
        }

        Vector2Int rotatedCell;

        switch (rotation % 360)
        {
            case 90:
                // 90° Rotation for 2x2 part
                rotatedCell = new Vector2Int(
                    partHeight - cell.y - 1,       // X = inverted Y
                    cell.x                         // Y = original X
                );
                break;

            case 180:
                // 180° Rotation
                rotatedCell = new Vector2Int(
                    partWidth - cell.x - 1,        // X = inverted X
                    partHeight - cell.y - 1        // Y = inverted Y
                );
                break;

            case 270:
                // 270° Rotation
                rotatedCell = new Vector2Int(
                    cell.y,                        // X = original Y
                    partWidth - cell.x - 1         // Y = inverted X
                );
                break;

            default:
                // No rotation: Return the local cell unchanged
                rotatedCell = cell;
                break;
        }

        return rotatedCell;
    }
    // Helper method to calculate direction offsets
    private Vector2Int DirectionToOffset(int direction)
    {
        switch (direction)
        {
            case 0: return new Vector2Int(0, -1); // North
            case 1: return new Vector2Int(1, 0);  // East
            case 2: return new Vector2Int(0, 1);  // South
            case 3: return new Vector2Int(-1, 0); // West
            default: return Vector2Int.zero; // Invalid direction
        }
    }

    // Helper method to convert direction into human-readable format
    private string GetHumanReadableDirection(int direction)
    {
        switch (direction)
        {
            case 0: return "North";
            case 1: return "East";
            case 2: return "South";
            case 3: return "West";
            default: return "Unknown";
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


                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }


    private void BuildGameGraph()
    {

        currPath = null;

        gameGraph = graphBuilder.BuildGraph(levelData.parts);

        pathFinder.Init(gameGraph);


    }

    //ensures i can edit loaded levels
    public void OnLoadLevel(List<PlacedPartInstance> loadedParts)
    {
        currPath=null;

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

        SplineHelper.CopySplinesToPlacedParts(levelData.parts, partsLibrary);
        
        //loop over all the loaded parts and feed them with their current data 
        foreach (var placedPart in levelData.parts)
        {
            TrackPart model = partsLibrary.Find(x => x.partName == placedPart.partType);

            placedPart.allowedPathsGroup = model.allowedPathsGroups;

            PopulatePlacedPartData(placedPart, model);
        }

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


    public static List<Vector2Int> GetOccupiedCells(PlacedPartInstance instance, List<TrackPart> _partsLibrary)
    {
        // Find the TrackPart model by name using LINQ's FirstOrDefault
        TrackPart model = _partsLibrary.FirstOrDefault(part => part.partName == instance.partType);
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
        // Normalize rotation to [0, 360)
        rotation = (rotation % 360 + 360) % 360;

        // Handle rotation based on part dimensions
        if (width != height)
        {
            switch (rotation)
            {
                case 0:
                    return offset;
                case 90:
                    // Explicit handling for W != H
                    return new Vector2Int(height - 1 - offset.y, offset.x);
                case 180:
                    return new Vector2Int(width - 1 - offset.x, height - 1 - offset.y);
                case 270:
                    // Explicit handling for W != H
                    return new Vector2Int(offset.y, width - 1 - offset.x);
                default:
                    UnityEngine.Debug.LogWarning("Unexpected rotation value");
                    return offset;
            }
        }
        else
        {
            // Standard square part handling
            switch (rotation)
            {
                case 0:
                    return offset;
                case 90:
                    return new Vector2Int(height - 1 - offset.y, offset.x);
                case 180:
                    return new Vector2Int(width - 1 - offset.x, height - 1 - offset.y);
                case 270:
                    return new Vector2Int(offset.y, width - 1 - offset.x);
                default:
                    UnityEngine.Debug.LogWarning("Unexpected rotation value");
                    return offset;
            }
        }
    }



    private void DrawPath(PathModel pathModel)
    {
        if (pathModel == null || !pathModel.Success) return;

        //Debug.Log("Start Path drawing");

        for (int i = 0; i < pathModel.Traversals.Count; i++)
        {
            var trav = pathModel.Traversals[i];
            PlacedPartInstance part = GetPartById(trav.partId);
            if (part == null) continue;

            bool simplePart = part.exits.Count <= 2;

            int splineIndex;
            float tStart;
            float tEnd;

            if (simplePart)
            {
                // SIMPLE PART
                if (trav.entryExit != -1 && trav.exitExit != -1)
                {
                    // Middle simple part → full spline
                    splineIndex = 0;
                    tStart = 0f;
                    tEnd = 1f;
                }
                else
                {
                    // First or last simple part → clamp one side at center (0.5), other by exit
                    splineIndex = 0;
                    float tEntry = (trav.entryExit == -1) ? 0.5f : GetExitT(part, trav.entryExit);
                    float tExit = (trav.exitExit == -1) ? 0.5f : GetExitT(part, trav.exitExit);

                    tStart = Mathf.Min(tEntry, tExit);
                    tEnd = Mathf.Max(tEntry, tExit);

                    if (Mathf.Approximately(tStart, tEnd))
                    {
                        const float eps = 0.001f;
                        if (tEnd + eps <= 1f) tEnd += eps; else tStart = Mathf.Max(0f, tStart - eps);
                    }
                }
            }
            else
            {
                // MULTI-EXIT PART
                splineIndex = FindAllowedPathIndex(part, trav.entryExit, trav.exitExit);

                // Always draw full sub-spline (we already chose the correct one)
                tStart = 0f;
                tEnd = 1f;
            }

            //Debug.Log($"Draw {part.partId} spl:{splineIndex} t[{tStart},{tEnd}] entry:{trav.entryExit} exit:{trav.exitExit}");
            DrawPathPreviewForPlacedPart2(part, splineIndex, tStart, tEnd);
        }

        //Debug.Log("End Path drawing");
    }


    private float GetExitT(PlacedPartInstance part, int exitIndex)
    {
        if (exitIndex < 0) return 0.5f;

        int dir = 0;
        for (int i = 0; i < part.exits.Count; i++)
            if (part.exits[i].exitIndex == exitIndex) { dir = part.exits[i].direction; break; }

        // Flipped: Up/Right = 0f, Down/Left = 1f
        switch (dir)
        {
            case 0: // Up
            case 1: // Right
                return 0f;
            case 2: // Down
            case 3: // Left
                return 1f;
            default:
                return 0.5f;
        }
    }



    // helper
    private int FindAllowedPathIndex(PlacedPartInstance part, int entryExit, int exitExit)
    {
        if (part.allowedPathsGroup == null) return 0;

        for (int g = 0; g < part.allowedPathsGroup.Count; g++)
        {
            var group = part.allowedPathsGroup[g];
            if (group.allowedPaths == null) continue;

            for (int a = 0; a < group.allowedPaths.Count; a++)
            {
                var ap = group.allowedPaths[a];
                if (ap.entryConnectionId == entryExit && ap.exitConnectionId == exitExit)
                    return g;   // <-- returns 1 for your (0,2) pair
            }
        }
        return 0;
    }

    private PlacedPartInstance GetPartById(string id)
    {
        for (int i = 0; i < levelData.parts.Count; i++)
            if (levelData.parts[i].partId == id)
                return levelData.parts[i];
        return null; // or throw/log
    }


    // === DRAW USING SAVED GRID-SPACE SPLINES ===
    // Assumes placed.splines[splineIndex] points are already in *grid coordinates*
    // (what you stored in gridCoordinatesSpline: rotated, offset, /cellSize).
    // We only convert grid -> GUI pixels: screen = gridRect.position + p * cellSize.

    private void DrawPathPreviewForPlacedPart2(PlacedPartInstance placed,
                                           int splineIndex,
                                           float tStart,
                                           float tEnd)
    {

        

        // Basic guards
        if (placed == null)
        {
            Debug.LogWarning("DrawPathPreviewForPlacedPart2: placed is null");
            return;
        }
        if (placed.bakedSplines == null)
        {
            Debug.LogWarning($"DrawPathPreviewForPlacedPart2: bakedSplines null for part {placed.partId}");
            return;
        }
        if (splineIndex < 0 || splineIndex >= placed.bakedSplines.Count)
        {
            Debug.LogWarning($"DrawPathPreviewForPlacedPart2: bad splineIndex {splineIndex} for part {placed.partId} (count {placed.bakedSplines.Count})");
            return;
        }

        var guiPts = placed.bakedSplines[splineIndex].guiPts;
        if (guiPts == null || guiPts.Count < 2)
        {
            Debug.LogWarning($"DrawPathPreviewForPlacedPart2: guiPts invalid (null or <2) for part {placed.partId}, spline {splineIndex}");
            return;
        }

        // Clamp/order
        float rawStart = tStart, rawEnd = tEnd;
        tStart = Mathf.Clamp01(tStart);
        tEnd = Mathf.Clamp01(tEnd);
        if (tEnd < tStart) { float tmp = tStart; tStart = tEnd; tEnd = tmp; }

        // Build segment
        var seg = ExtractSegmentScreen(guiPts, tStart, tEnd);
        if (seg.Count < 2)
        {
            Debug.LogWarning($"DrawPathPreviewForPlacedPart2: seg.Count < 2 for part {placed.partId}, spline {splineIndex} (tStart={tStart}, tEnd={tEnd})");
            return;
        }

        // ---- DEBUG DUMP ----
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== DrawPathPreviewForPlacedPart2 DEBUG ===");
        sb.AppendLine($"Part: {placed.partId}   SplineIndex: {splineIndex}");
        sb.AppendLine($"Raw tStart/tEnd: {rawStart} / {rawEnd}   Clamped: {tStart} / {tEnd}");
        sb.AppendLine($"guiPts.Count: {guiPts.Count}");
        sb.AppendLine($"First guiPt: {guiPts[0]}   Last guiPt: {guiPts[guiPts.Count - 1]}");
        sb.AppendLine($"seg.Count: {seg.Count}");
        sb.AppendLine($"seg[0]: {seg[0]}   seg[last]: {seg[seg.Count - 1]}");
        // Print a few mid points (limit to avoid spam)
        int toPrint = Mathf.Min(seg.Count, 6);
        for (int i = 0; i < toPrint; i++)
            sb.AppendLine($" seg[{i}]: {seg[i]}");
        //Debug.Log(sb.ToString());
        // ---------------------

        // Draw
        Handles.color = Color.yellow;
        Handles.DrawAAPolyLine(4f, seg.ToArray());
        Handles.DrawSolidDisc(seg[0], Vector3.forward, 4f);
        Handles.DrawSolidDisc(seg[seg.Count - 1], Vector3.forward, 4f);
    }


    private List<Vector3> ExtractSegmentScreen(List<Vector2> pts, float tStart, float tEnd)
    {
        float total = 0f;
        float[] cum = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++)
        {
            total += Vector2.Distance(pts[i - 1], pts[i]);
            cum[i] = total;
        }
        if (total <= 0f) return new List<Vector3> { pts[0], pts[pts.Count - 1] };

        float sLen = tStart * total;
        float eLen = tEnd * total;

        Vector2 PointAt(float d)
        {
            for (int i = 1; i < pts.Count; i++)
            {
                float a = cum[i - 1], b = cum[i];
                if (d <= b)
                {
                    float u = Mathf.InverseLerp(a, b, d);
                    return Vector2.Lerp(pts[i - 1], pts[i], u);
                }
            }
            return pts[pts.Count - 1];
        }

        var outPts = new List<Vector3>();
        outPts.Add(PointAt(sLen));
        for (int i = 1; i < pts.Count - 1; i++)
            if (cum[i] > sLen && cum[i] < eLen) outPts.Add(pts[i]);
        outPts.Add(PointAt(eLen));
        return outPts;
    }


}