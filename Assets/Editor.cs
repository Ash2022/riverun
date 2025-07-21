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

    GraphModel graphModel;
    GameGraph gameGraph;

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

        //pathFinder = new PathFinder(trackGraph);
        gameGraph = new GameGraph();
        pathVisualizer = new PathVisualizer();

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

            /*
            if (_previewPath != null)
            {
                DrawPathPreview(_previewPath);
            }*/
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

                int startExitIdx = 0; // Or whichever exit you want to start from
                int endExitIdx = 0;   // Or whichever exit you want to reach at the destination

                if (startPart != null && endPart != null)
                {
                    //_previewPath = pathFinder.FindPath(startPart, startExitIdx, endPart, endExitIdx,partsLibrary);
                    
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

    private List<List<Vector2>> DrawSplinesForPart(TrackPart part, PlacedPartInstance placed, Rect partRect, bool buildOnly = false)
    {
        // Calculate offsets for visual alignment when W != H
        float offsetX2 = 0f, offsetY2 = 0f;
        if (part.gridWidth != part.gridHeight)
        {
            if (placed.rotation % 360 == 90)
            {
                offsetX2 = (part.gridHeight - part.gridWidth) * cellSize / -2f;
                offsetY2 = (part.gridWidth - part.gridHeight) * cellSize / 2f;
            }
            else if (placed.rotation % 360 == 270)
            {
                offsetX2 = (part.gridHeight - part.gridWidth) * cellSize / 2f;
                offsetY2 = (part.gridHeight - part.gridWidth) * cellSize / 2f;
            }
        }

        // Initialize the list to return spline data
        List<List<Vector2>> splineData = new List<List<Vector2>>();
        var splines = part.GetSplinesAsVector2();

        for (int i = 0; i < splines.Count; i++)
        {
            var spline = splines[i];
            Vector3[] pts = new Vector3[spline.Count];
            List<Vector2> gridCoordinatesSpline = new List<Vector2>();

            for (int j = 0; j < spline.Count; j++)
            {
                Vector2 pt = spline[j];
                float gx = pt.x * cellSize;
                float gy = pt.y * cellSize;

                // Adjust spline points for visual offsets
                Vector2 gridPt = new Vector2(partRect.x + gx + offsetX2, partRect.y + gy + offsetY2);
                Vector2 partCenter = new Vector2(partRect.x + partRect.width / 2f, partRect.y + partRect.height / 2f);

                // Rotate spline points around the part's center
                Vector2 rotatedPt = RotatePointAround(gridPt, partCenter, placed.rotation);
                pts[j] = rotatedPt;

                // Convert rotated point to grid coordinates and add to spline list
                gridCoordinatesSpline.Add(new Vector2(rotatedPt.x / cellSize, rotatedPt.y / cellSize));
            }

            // Add the spline data to the return list
            splineData.Add(gridCoordinatesSpline);

            // Only draw if buildOnly is false (default behavior)
            if (!buildOnly)
            {
                // Draw the spline
                Handles.DrawAAPolyLine(4f, pts);

                // Draw markers for the start and end points of the spline
                Handles.DrawSolidDisc(pts.First(), Vector3.forward, 4f);
                Handles.DrawSolidDisc(pts.Last(), Vector3.forward, 4f);
            }
        }

        return splineData;
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

                    List<AllowedPath> instanceAllowedPaths = new List<AllowedPath>();

                    foreach (AllowedPathGroup allowedPathGroup in selectedPart.allowedPaths)
                        instanceAllowedPaths.AddRange(allowedPathGroup.allowedPaths);
                    
                    var newInstance = new PlacedPartInstance
                    {
                        allowedPaths = instanceAllowedPaths,
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
        instance.splines = DrawSplinesForPart(model, instance, partRect, true);

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

        Debug.Log($"--- End of Part Details ---");

        Debug.Log($"Spline values updated successfully in PlacedPartInstance.");
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


                //List<PathPartConnection> connections = PathTrackGraphBuilder.BuildConnectionsFromGrid(levelData.parts, partsLibrary);

                //trackGraph = PathTrackGraphBuilder.BuildPathTrackGraph(levelData.parts,partsLibrary, connections);

                //pathFinder = new PathFinder(trackGraph);

                Repaint();
            }
        }
        EditorGUILayout.EndHorizontal();
    }


    private void BuildGameGraph()
    {
        // Build connections
        List<Connection> connections = ConnectionBuilder.BuildConnections(levelData.parts);

        // Print connections
        foreach (var connection in connections)
        {
            Debug.Log(connection.ToString());
        }

        graphModel = gameGraph.BuildGraph(connections);

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

        SplineHelper.CopySplinesToPlacedParts(levelData.parts, partsLibrary);
        
        //loop over all the loaded parts and feed them with their current data 
        foreach (var placedPart in levelData.parts)
        {
            TrackPart model = partsLibrary.Find(x => x.partName == placedPart.partType);

            List<AllowedPath> instanceAllowedPaths = new List<AllowedPath>();

            foreach (AllowedPathGroup allowedPathGroup in model.allowedPaths)
                instanceAllowedPaths.AddRange(allowedPathGroup.allowedPaths);

            placedPart.allowedPaths = instanceAllowedPaths;

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

    /*
    private void DrawPathPreview(List<PathSegment> path)
    {

        Debug.Log($"START PATH");

        Handles.BeginGUI();
        Handles.color = Color.yellow;
        
        for (int i = 0; i < path.Count; i++)
        {

            var segment = path[i];
            if (segment.placedPart == null) continue;

            float tStart, tEnd;

            if (i == 0)
            {
                // Start part: center to exit
                tStart = 0.5f;
                tEnd = (segment.exitIdx == 0) ? 1f : 0f;

                Debug.Log($"START SEGMENT: PlacedPartName={segment.placedPart.partId.ToString()}, tStart={tStart}, tEnd={tEnd}");
            }
            else if (i == path.Count - 1)
            {
                // End part: entrance to center
                tStart = (segment.entranceExitIdx == 0) ? 0f : 1f;
                tEnd = 0.5f;


                Debug.Log($"END SEGMENT: PlacedPartName={segment.placedPart.partId.ToString()}, tStart={tStart}, tEnd={tEnd}");
            }
            else
            {
                
                // Middle parts: use tStart and tEnd from the segment
                tStart = segment.tStart;
                tEnd = segment.tEnd;
            }

            DrawPathPreviewForPlacedPart(segment.placedPart, segment.splineIndex, tStart, tEnd);
        }
        Debug.Log($"END PATH");
        Handles.EndGUI();
    }
    */

    void DrawGUILine(Vector2 pointA, Vector2 pointB, Color color, float thickness)
    {
        // Round coordinates to integer pixel values for crisp lines
        Vector2 pA = new Vector2(Mathf.RoundToInt(pointA.x), Mathf.RoundToInt(pointA.y));
        Vector2 pB = new Vector2(Mathf.RoundToInt(pointB.x), Mathf.RoundToInt(pointB.y));

        Color oldColor = GUI.color;
        GUI.color = color;

        // If the line is strictly vertical
        if (Mathf.Approximately(pA.x, pB.x))
        {
            float minY = Mathf.Min(pA.y, pB.y);
            float maxY = Mathf.Max(pA.y, pB.y);
            // Overlap endpoints by half thickness
            minY -= thickness / 2f;
            maxY += thickness / 2f;
            GUI.DrawTexture(new Rect(pA.x - thickness / 2f, minY, thickness, maxY - minY), EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;
            return;
        }

        // If strictly horizontal
        if (Mathf.Approximately(pA.y, pB.y))
        {
            float minX = Mathf.Min(pA.x, pB.x);
            float maxX = Mathf.Max(pA.x, pB.x);
            minX -= thickness / 2f;
            maxX += thickness / 2f;
            GUI.DrawTexture(new Rect(minX, pA.y - thickness / 2f, maxX - minX, thickness), EditorGUIUtility.whiteTexture);
            GUI.color = oldColor;
            return;
        }

        // For diagonal: use rotated rectangle with overlap
        Vector2 delta = pB - pA;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float length = delta.magnitude + thickness; // overlap endpoints

        Matrix4x4 matrix = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, pA);
        GUI.DrawTexture(new Rect(pA.x, pA.y - thickness / 2f, length, thickness), EditorGUIUtility.whiteTexture);
        GUI.matrix = matrix;
        GUI.color = oldColor;
    }



    private void DrawPathPreviewForPlacedPart(PlacedPartInstance placed, int splineIndex, float tStart = 0f, float tEnd = 1f)
    {
        TrackPart part = partsLibrary.Find(p => p.partName == placed.partType);
        if (part == null)
        {
            Debug.Log("NoParrt - leaving");
            return;
        }

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
            if (rotation == 270)
            {
                offsetX = (h - w) / 2f * cellSize;
                offsetY = (w - h) / 2f * cellSize;
            }
        }

        Rect partRect = new Rect(px + offsetX, py - offsetY, pw, ph);
        Handles.color = Color.yellow;

        var splineArr = part.splineTemplates[splineIndex];
        int numPoints = splineArr.Count;
        if (numPoints < 2) return;

        // Clamp tStart and tEnd
        float tS = Mathf.Clamp01(tStart);
        float tE = Mathf.Clamp01(tEnd);

        // Swap if needed so we always draw from lower to higher
        bool reversed = false;
        if (tE < tS)
        {
            float temp = tS;
            tS = tE;
            tE = temp;
            reversed = true;
        }

        Vector2 partCenter = new Vector2(partRect.x + partRect.width / 2f, partRect.y + partRect.height / 2f);

        // Sample along the spline (linear interpolation between control points)
        int steps = Mathf.Max(2, Mathf.CeilToInt((tE - tS) * numPoints / 0.05f)); // or just use a fixed step

        List<Vector3> pts = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float t = Mathf.Lerp(tS, tE, i / (float)steps);

            // Find segment and interpolate
            float totalT = t * (numPoints - 1);
            int idx = Mathf.FloorToInt(totalT);
            float frac = totalT - idx;

            // Fix: when we're at the very end, interpolate to the last segment using frac=1.0
            if (idx >= numPoints - 1)
            {
                idx = numPoints - 2;
                frac = 1f;
            }

            Vector2 p0 = new Vector2(splineArr[idx][0], splineArr[idx][1]);
            Vector2 p1 = new Vector2(splineArr[idx + 1][0], splineArr[idx + 1][1]);
            Vector2 pt = Vector2.Lerp(p0, p1, frac);

            float gx = pt.x * cellSize;
            float gy = pt.y * cellSize;
            Vector2 gridPt = new Vector2(partRect.x + gx, partRect.y + gy);
            Vector2 rotatedPt = RotatePointAround(gridPt, partCenter, rotation);

            // Debug log for each step
/*
            Debug.Log(
                $"Step {i}/{steps}: t={t:F3}, totalT={totalT:F3}, idx={idx}, frac={frac:F3}\n" +
                $"p0=({p0.x:F3},{p0.y:F3}), p1=({p1.x:F3},{p1.y:F3}), pt=({pt.x:F3},{pt.y:F3})\n" +
                $"gridPt=({gridPt.x:F2},{gridPt.y:F2}), rotatedPt=({rotatedPt.x:F2},{rotatedPt.y:F2})"
            );*/

            pts.Add(rotatedPt);
        }
        if (pts.Count >= 2)
        {
            //Debug.Log($"DrawPathPreviewForPlacedPart: PlacedPartName={placed.partId.ToString()}, splineIndex={splineIndex}, tStart={tStart}, tEnd={tEnd}, tS={tS}, tE={tE}, reversed={reversed}");

            Handles.DrawAAPolyLine(20f, pts.ToArray());
        }
    }

}