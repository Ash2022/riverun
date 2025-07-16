using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;
using System.Linq;


public class PartSplinePreviewWindow : EditorWindow
{
    private List<TrackPart> parts;
    private int selectedPartIndex = 0;
    private Vector2 scroll;
    private const int cellSize = 64;

    // Spline editing state
    private bool editSplineMode = false;
    private int editingSplineIndex = 0;
    private int draggingPointIndex = -1;
    private Vector2 dragStartMouse;
    private Vector2 dragStartPos;

    [MenuItem("Tools/Part Spline Preview")]
    public static void ShowWindow()
    {
        GetWindow<PartSplinePreviewWindow>("Part Spline Preview");
    }

    private void OnEnable()
    {
        LoadParts();
    }

    private void LoadParts()
    {
        parts = new List<TrackPart>();
        TextAsset jsonText = Resources.Load<TextAsset>("parts");
        if (jsonText != null)
        {
            parts = JsonConvert.DeserializeObject<List<TrackPart>>(jsonText.text);

            foreach (var part in parts)
            {
                EnsureSplinesExistForPart(part);
            }
        }
        else
        {
            Debug.LogError("Could not find parts.json in Resources.");
        }
    }

    // Ensure each allowedPath has a spline in splinePointsList, endpoints match connections
    private void EnsureSplinesExistForPart(TrackPart part)
    {
        // Upgrade data model if needed
        if (part.splineTemplates == null)
            part.splineTemplates = new List<List<Vector2>>();

        // Remove excess splines
        if (part.splineTemplates.Count > part.allowedPaths.Count)
            part.splineTemplates.RemoveRange(part.allowedPaths.Count, part.splineTemplates.Count - part.allowedPaths.Count);

        // Add missing splines
        for (int i = 0; i < part.allowedPaths.Count; i++)
        {
            if (i >= part.splineTemplates.Count || part.splineTemplates[i] == null || part.splineTemplates[i].Count < 2)
            {
                var path = part.allowedPaths[i];
                var entryConn = part.connections.FirstOrDefault(c => c.id == path.entryConnectionId);
                var exitConn = part.connections.FirstOrDefault(c => c.id == path.exitConnectionId);

                Vector2 start = GetConnectionLocalGrid(part, entryConn);
                Vector2 end = GetConnectionLocalGrid(part, exitConn);

                part.splineTemplates.Add(new List<Vector2> { start, end });
            }
            else
            {
                // Force endpoints to match connections
                var path = part.allowedPaths[i];
                var entryConn = part.connections.FirstOrDefault(c => c.id == path.entryConnectionId);
                var exitConn = part.connections.FirstOrDefault(c => c.id == path.exitConnectionId);

                List<Vector2> spline = part.splineTemplates[i];
                if (spline.Count >= 2)
                {
                    spline[0] = GetConnectionLocalGrid(part, entryConn);
                    spline[spline.Count - 1] = GetConnectionLocalGrid(part, exitConn);
                }
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Part", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(120));
        for (int i = 0; i < parts.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Toggle(selectedPartIndex == i, "", GUILayout.Width(20)))
                selectedPartIndex = i;
            EditorGUILayout.LabelField(parts[i].partName, GUILayout.Width(160));
            Texture2D img = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(parts[i].displaySprite));
            if (img != null)
                GUILayout.Label(img, GUILayout.Width(32), GUILayout.Height(32));
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);

        if (parts == null || parts.Count == 0)
        {
            GUILayout.Label("No parts loaded!");
            return;
        }

        TrackPart part = parts[selectedPartIndex];
        GUILayout.Label($"Part: {part.partName} [{part.gridWidth}x{part.gridHeight}]");

        // Spline edit toggle
        editSplineMode = GUILayout.Toggle(editSplineMode, "Edit Spline Mode", EditorStyles.toolbarButton);

        // Only draw 0 rotation preview
        float previewWidth = part.gridWidth * cellSize;
        float previewHeight = part.gridHeight * cellSize;
        Rect previewRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));

        Texture2D imgSpr = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(part.displaySprite));
        if (imgSpr != null)
            GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, previewRect.width, previewRect.height), imgSpr, ScaleMode.StretchToFill);

        // Draw connections (0 rotation)
        var connPos = new Dictionary<int, Vector2>();
        foreach (var c in part.connections)
        {
            Vector2 pos = GetConnectionScreen(previewRect, part, c);
            connPos[c.id] = pos;
        }

        Handles.BeginGUI();
        Handles.color = Color.cyan;
        for (int p = 0; p < part.allowedPaths.Count; p++)
        {
            var path = part.allowedPaths[p];
            Vector2 from = connPos[path.entryConnectionId];
            Vector2 to = connPos[path.exitConnectionId];
            Handles.DrawAAPolyLine(3f, from, to);
            Handles.DrawSolidDisc(from, Vector3.forward, 5f);
            Handles.DrawSolidDisc(to, Vector3.forward, 5f);
        }
        Handles.color = Color.yellow;
        foreach (var kvp in connPos)
        {
            Handles.DrawSolidDisc(kvp.Value, Vector3.forward, 4f);
            Handles.Label(kvp.Value + Vector2.up * 12, $"ID {kvp.Key}", EditorStyles.boldLabel);
        }

        // Spline editing mode
        if (editSplineMode)
        {
            if (part.allowedPaths.Count > 1)
            {
                GUILayout.Label("Editing Path:");
                string[] pathLabels = part.allowedPaths.Select((ap, idx) =>
                {
                    var entry = part.connections.FirstOrDefault(c => c.id == ap.entryConnectionId);
                    var exit = part.connections.FirstOrDefault(c => c.id == ap.exitConnectionId);
                    return $"Path {idx}: {entry?.id} → {exit?.id}";
                }).ToArray();
                editingSplineIndex = GUILayout.SelectionGrid(editingSplineIndex, pathLabels, 1);
            }
            Handles.color = Color.magenta;

            // Change here: splinePointsList → splineTemplates
            if (part.splineTemplates == null || editingSplineIndex >= part.splineTemplates.Count)
                EnsureSplinesExistForPart(part);

            List<Vector2> spline = part.splineTemplates[editingSplineIndex];

            // Draw spline lines
            Vector3[] linePts = spline.Select(pt => (Vector3)SplineLocalToScreen(previewRect, part, pt)).ToArray();
            Handles.DrawAAPolyLine(4f, linePts);

            // Draw and handle points (start/end locked)
            for (int sp = 0; sp < spline.Count; sp++)
            {
                bool canDrag = (sp != 0 && sp != spline.Count - 1); // endpoints locked
                Vector2 pt = SplineLocalToScreen(previewRect, part, spline[sp]);
                Handles.DrawSolidDisc(pt, Vector3.forward, 8f);
                Handles.Label(pt + Vector2.up * 14, $"P{sp}", EditorStyles.boldLabel);

                Rect hitRect = new Rect(pt.x - 10, pt.y - 10, 20, 20);
                if (canDrag && draggingPointIndex == -1 && Event.current.type == EventType.MouseDown && hitRect.Contains(Event.current.mousePosition))
                {
                    draggingPointIndex = sp;
                    dragStartMouse = Event.current.mousePosition;
                    dragStartPos = spline[sp];
                    Event.current.Use();
                }
                else if (canDrag && draggingPointIndex == sp && Event.current.type == EventType.MouseDrag)
                {
                    Vector2 mouseDelta = Event.current.mousePosition - dragStartMouse;
                    Vector2 newPt = dragStartPos + ScreenToSplineDelta(mouseDelta, previewRect, part);
                    newPt.x = Mathf.Clamp(newPt.x, 0, part.gridWidth);
                    newPt.y = Mathf.Clamp(newPt.y, 0, part.gridHeight);
                    spline[sp] = newPt;
                    Repaint();
                    Event.current.Use();
                }
                else if (draggingPointIndex == sp && Event.current.type == EventType.MouseUp)
                {
                    draggingPointIndex = -1;
                    Event.current.Use();
                }
            }

            // Add point on spline: click near it, but not on a control point
            if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
            {
                Vector2 clickPos = ScreenToSpline(Event.current.mousePosition, previewRect, part);

                bool onControl = false;
                foreach (var pt in spline)
                {
                    if ((SplineLocalToScreen(previewRect, part, pt) - Event.current.mousePosition).magnitude < 14f)
                    {
                        onControl = true; break;
                    }
                }
                if (!onControl)
                {
                    // Find closest segment
                    int insertIndex = -1;
                    float minDist = float.MaxValue;
                    Vector2 closestScreen = Vector2.zero;
                    for (int i = 0; i < spline.Count - 1; i++)
                    {
                        Vector2 p0 = spline[i];
                        Vector2 p1 = spline[i + 1];
                        Vector2 screenP0 = SplineLocalToScreen(previewRect, part, p0);
                        Vector2 screenP1 = SplineLocalToScreen(previewRect, part, p1);
                        Vector2 closest = ClosestPointOnSegmentScreen(Event.current.mousePosition, screenP0, screenP1);
                        float dist = (Event.current.mousePosition - closest).magnitude;
                        if (dist < minDist)
                        {
                            minDist = dist;
                            insertIndex = i + 1;
                            closestScreen = closest;
                        }
                    }
                    if (minDist < 18f)
                    {
                        Vector2 splineSpace = ScreenToSpline(closestScreen, previewRect, part);
                        spline.Insert(insertIndex, splineSpace);
                        Repaint();
                        Event.current.Use();
                    }
                }
            }
        }

        Handles.EndGUI();

        GUILayout.Space(12);

        if (GUILayout.Button("Save New Parts"))
        {
            string path = EditorUtility.SaveFilePanel("Save Parts JSON", Application.dataPath, "parts.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(parts, Formatting.Indented));
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Saved", "Parts saved to:\n" + path, "OK");
            }
        }
    }

    // Spline point [0,w],[0,h] to screen pixel in previewRect
    private Vector2 SplineLocalToScreen(Rect previewRect, TrackPart part, Vector2 pt)
    {
        return new Vector2(previewRect.x + pt.x * cellSize, previewRect.y + pt.y * cellSize);
    }

    // Screen pixel to spline [0,w],[0,h]
    private Vector2 ScreenToSpline(Vector2 screenPt, Rect previewRect, TrackPart part)
    {
        float x = (screenPt.x - previewRect.x) / cellSize;
        float y = (screenPt.y - previewRect.y) / cellSize;
        return new Vector2(x, y);
    }

    // Mouse drag delta from screen to spline space
    private Vector2 ScreenToSplineDelta(Vector2 delta, Rect previewRect, TrackPart part)
    {
        return new Vector2(delta.x / cellSize, delta.y / cellSize);
    }

    // Closest point on a segment to p (screen space)
    private Vector2 ClosestPointOnSegmentScreen(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Vector2.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        return a + t * ab;
    }

    // For all grid sizes, use connection direction for edge positions
    private Vector2 GetConnectionLocalGrid(TrackPart part, PartConnection conn)
    {
        if (conn == null) return new Vector2(0.5f, 0.5f);

        if (part.gridWidth == 1 && part.gridHeight == 1)
        {
            switch (conn.direction)
            {
                case 0: return new Vector2(0.5f, 0f);   // Up
                case 1: return new Vector2(1f, 0.5f);   // Right
                case 2: return new Vector2(0.5f, 1f);   // Down
                case 3: return new Vector2(0f, 0.5f);   // Left
            }
        }
        // Multi-cell: Use center of connection cell, then offset to edge by direction
        float cellX = conn.gridOffset[0] + 0.5f;
        float cellY = conn.gridOffset[1] + 0.5f;
        float offsetX = 0, offsetY = 0;
        switch (conn.direction)
        {
            case 0: offsetY = -0.5f; break; // Up
            case 1: offsetX = 0.5f; break;  // Right
            case 2: offsetY = 0.5f; break;  // Down
            case 3: offsetX = -0.5f; break; // Left
        }
        return new Vector2(cellX + offsetX, cellY + offsetY);
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
        return new Vector2(cx, cy); // Center fallback
    }

    private Vector2 GetConnectionScreen(Rect previewRect, TrackPart part, PartConnection conn)
    {
        Vector2 local = GetConnectionLocalGrid(part, conn);
        return SplineLocalToScreen(previewRect, part, local);
    }

    /// Rotates a point around pivot by angle in degrees
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
}