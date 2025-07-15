using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;

[System.Serializable]
public class PartConnection
{
    public int id;
    public int direction;
    public int[] gridOffset; // [x, y]
}

[System.Serializable]
public class AllowedPath
{
    public int entryConnectionId;
    public int exitConnectionId;
}

[System.Serializable]
public class TrackPart
{
    public string partName;
    public string trackType;
    public int gridWidth;
    public int gridHeight;
    public string displaySprite;
    public List<PartConnection> connections;
    public List<AllowedPath> allowedPaths;
}

public class PartSplinePreviewWindow : EditorWindow
{
    private List<TrackPart> parts;
    private int selectedPartIndex = 0;
    private Vector2 scroll;
    private const int cellSize = 64;

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
        }
        else
        {
            Debug.LogError("Could not find parts.json in Resources.");
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

        int rotations = 4;
        float previewWidth = part.gridWidth * cellSize;
        float previewHeight = part.gridHeight * cellSize;
        float spaceBetween = 20f;

        float totalWidth = rotations * previewWidth + (rotations - 1) * spaceBetween;
        Rect previewsRect = GUILayoutUtility.GetRect(totalWidth, previewHeight, GUILayout.Width(totalWidth), GUILayout.Height(previewHeight));

        Texture2D imgSpr = Resources.Load<Texture2D>("Images/" + System.IO.Path.GetFileNameWithoutExtension(part.displaySprite));

        for (int rot = 0; rot < rotations; rot++)
        {
            float left = previewsRect.x + rot * (previewWidth + spaceBetween);
            Rect previewRect = new Rect(left, previewsRect.y, previewWidth, previewHeight);

            // Draw Sprite (rotated)
            if (imgSpr != null)
            {
                Matrix4x4 oldMatrix = GUI.matrix;
                Vector2 pivot = new Vector2(previewRect.x + previewRect.width / 2f, previewRect.y + previewRect.height / 2f);
                GUIUtility.RotateAroundPivot(rot * 90, pivot);
                GUI.DrawTexture(new Rect(previewRect.x, previewRect.y, previewRect.width, previewRect.height), imgSpr, ScaleMode.StretchToFill);
                GUI.matrix = oldMatrix;
            }

            // Build connection positions dictionary (rotated!)
            var connPos = new Dictionary<int, Vector2>();

            foreach (var c in part.connections)
            {
                Vector2 pos;

                if (part.gridWidth == 1 && part.gridHeight == 1)
                {
                    // 1x1: Use the edge of the part, rotated direction
                    pos = GetConnectionPosition1x1(previewRect, c.direction, rot);
                }
                else
                {
                    // For NxM, cell center offset to edge by ORIGINAL direction, then rotate
                    float cellCenterX = previewRect.x + (c.gridOffset[0] + 0.5f) * cellSize;
                    float cellCenterY = previewRect.y + (c.gridOffset[1] + 0.5f) * cellSize;

                    float offsetX = 0, offsetY = 0;
                    switch (c.direction)
                    {
                        case 0: offsetY = -cellSize / 2f; break; // Up
                        case 1: offsetX = cellSize / 2f; break;  // Right
                        case 2: offsetY = cellSize / 2f; break;  // Down
                        case 3: offsetX = -cellSize / 2f; break; // Left
                    }
                    Vector2 cellPos = new Vector2(cellCenterX + offsetX, cellCenterY + offsetY);

                    // Rotate around part center
                    Vector2 partCenter = new Vector2(previewRect.x + previewRect.width / 2f, previewRect.y + previewRect.height / 2f);
                    pos = RotatePointAround(cellPos, partCenter, rot * 90);
                }
                connPos[c.id] = pos;
            }

            // Draw Splines for Allowed Paths
            Handles.BeginGUI();
            Handles.color = Color.cyan;

            foreach (var path in part.allowedPaths)
            {
                Vector2 from = connPos[path.entryConnectionId];
                Vector2 to = connPos[path.exitConnectionId];

                Handles.DrawAAPolyLine(3f, from, to);
                Handles.DrawSolidDisc(from, Vector3.forward, 5f);
                Handles.DrawSolidDisc(to, Vector3.forward, 5f);
            }

            Handles.color = Color.yellow;
            // Draw all connection points
            foreach (var kvp in connPos)
            {
                Handles.DrawSolidDisc(kvp.Value, Vector3.forward, 4f);
                Handles.Label(kvp.Value + Vector2.up * 12, $"ID {kvp.Key}", EditorStyles.boldLabel);
            }

            Handles.EndGUI();
        }
    }

    // 1x1 part connection position at edge for direction+rotation
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