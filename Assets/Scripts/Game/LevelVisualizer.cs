using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LevelVisualizer : MonoBehaviour
{
    public static LevelVisualizer Instance { get; private set; }

    [SerializeField] private TextAsset partsJson;
    private List<TrackPart> partsLibrary;
    [SerializeField] public List<Sprite> partSprites;  // must match partsLibrary order

    [HideInInspector] private float cellSize;

    [Header("Data")]
    [SerializeField] private TextAsset levelJson;

    [Header("Prefabs & Parents")]
    [SerializeField] private GameObject partPrefab;
    [SerializeField] private Transform mainHolder;

    [Header("Frame & Build Settings")]
    [SerializeField] private SpriteRenderer frameRenderer;
    [SerializeField] private float tileDelay = 0.05f;

    [SerializeField] float frameWidthUnits = 9f;
    [SerializeField] float frameHeightUnits = 16f;

    [SerializeField] LineRenderer globalPathRenderer;
    public float CellSize { get => cellSize; set => cellSize = value; }
    public float MAX_CELL_SIZE = 100;

    void Awake()
    {
        Instance = this;
        partsLibrary = JsonConvert.DeserializeObject<List<TrackPart>>(partsJson.text);
    }

    void Start()
    {
        Build();
    }


    /// <summary>
    /// Call this to (re)build the entire level.
    /// </summary>
    public void Build()
    {
        var settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new Vector2Converter() },
            Formatting = Formatting.Indented
        };

        // clear out any previously spawned parts
        for (int i = mainHolder.childCount - 1; i >= 0; i--)
            DestroyImmediate(mainHolder.GetChild(i).gameObject);

        if (levelJson == null || partPrefab == null || mainHolder == null)
        {
            Debug.LogError("LevelVisualizer: missing references.");
            return;
        }

        LevelData level;
        try
        {
            level = JsonConvert.DeserializeObject<LevelData>(levelJson.text, settings);
        }
        catch
        {
            Debug.LogError("LevelVisualizer: failed to parse LevelData JSON.");
            return;
        }

        if (level.parts == null || level.parts.Count == 0)
        {
            Debug.LogWarning("LevelVisualizer: no parts in level.");
            return;
        }

        StartCoroutine(BuildCoroutine(level));
    }

    private IEnumerator BuildCoroutine(LevelData level)
    {
        // compute grid bounds

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var inst in level.parts)
            foreach (var cell in inst.occupyingCells)
            {
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }
        int gridW = maxX - minX + 1;
        int gridH = maxY - minY + 1;
        /*
        Vector2 worldOrigin;
        {
            // use fixed logical frame size
            float sizeX2 = frameWidthUnits / gridW;
            float sizeY2 = frameHeightUnits / gridH;

            // pick the larger so we fill (and potentially overflow) one axis
            cellSize = Mathf.Max(sizeX2, sizeY2);

            cellSize = Mathf.Min(MAX_CELL_SIZE, cellSize);  

            // compute grid's half‑size in world units
            Vector2 halfGrid = new Vector2(gridW, gridH) * cellSize * 0.5f;

            // assume the frame's center == this.transform.position
            Vector2 frameCenter = (Vector2)transform.position;

            // origin is bottom‑left of grid: center minus halfGrid, plus half a cell
            worldOrigin = frameCenter - halfGrid + Vector2.one * (cellSize * 0.5f);
        }
        */

        // determine cellSize and worldOrigin so that the grid is centered in the frame
        Bounds fb = frameRenderer.bounds;
        float frameW = fb.size.x;
        float frameH = fb.size.y;

        // how big each cell would be to exactly fill width or height
        float sizeX = frameW / gridW;
        float sizeY = frameH / gridH;

        // pick the *smaller* so that the entire grid fits inside the frame
        cellSize = Mathf.Min(sizeX, sizeY, MAX_CELL_SIZE);

        Debug.Log("CellSize: " + cellSize);

        // now compute the *actual* size the grid will occupy
        float gridWorldW = cellSize * gridW;
        float gridWorldH = cellSize * gridH;

        // find the bottom‑left corner of the grid inside the frame
        Vector3 frameMin = fb.min; // bottom‑left corner of the frame in world coords
                                   // inset so that the grid is centered: we leave half of (frameSize − gridSize) as margin on each side
        float marginX = (frameW - gridWorldW) * 0.5f;
        float marginY = (frameH - gridWorldH) * 0.5f;

        // worldOrigin is the world position of grid cell (0,0)
        Vector2 worldOrigin = new Vector2(frameMin.x + marginX,
                                  frameMin.y + marginY);

        foreach (var inst in level.parts)
        {
            // 1) find the bounding box of the occupied cells
            int minCX = int.MaxValue, minCY = int.MaxValue;
            int maxCX = int.MinValue, maxCY = int.MinValue;
            foreach (var c in inst.occupyingCells)
            {
                minCX = Mathf.Min(minCX, c.x);
                minCY = Mathf.Min(minCY, c.y);
                maxCX = Mathf.Max(maxCX, c.x);
                maxCY = Mathf.Max(maxCY, c.y);
            }

            // 2) compute the true geometric center of that box (cells are inclusive)
            //    +1 so that, e.g., min=1,max=2 → 2 cells wide, centre at (1+2+1)/2 = 2
            float centerX = (minCX + maxCX + 1) * 0.5f - minX;
            float centerY = (minCY + maxCY + 1) * 0.5f - minY;

            // 3) flip Y (so world (0,0) is bottom‑left of grid)
            Vector2 flipped = new Vector2(centerX, gridH - centerY);

            // 4) convert to world coords
            Vector3 pos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );

            // spawn & orient
            var go = Instantiate(partPrefab, mainHolder);
            go.name = inst.partId;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -inst.rotation);

            // copy spline templates into the instance for the view
            TrackPart trackPart = partsLibrary.Find(x => x.partName == inst.partType);
            inst.splines = trackPart.splineTemplates;

            // hand off to view
            if (go.TryGetComponent<TrackPartView>(out var view))
                view.Setup(inst);

            yield return new WaitForSeconds(tileDelay);
        }

        DrawGlobalSplinePath(level.parts);
    }

    /// <summary>  
    /// Returns the sprite for the given partType, or null if not found.  
    /// </summary>
    public Sprite GetSpriteFor(string partType)
    {
        int idx = partsLibrary.FindIndex(p => p.partName == partType);
        return (idx >= 0 && idx < partSprites.Count) ? partSprites[idx] : null;
    }


    /// <summary>
    /// Grabs spline #0 from each part in order, transforms into world‐space, and feeds them
    /// into one long LineRenderer.
    /// </summary>
    private void DrawGlobalSplinePath(List<PlacedPartInstance> levelParts)
    {
        var worldPts = new List<Vector3>();

        foreach (var inst in levelParts)
        {
            if (inst.splines == null || inst.splines.Count == 0)
                continue;

            // 1) first spline in grid‐space
            var spline = inst.splines[0];

            // 2) find its GameObject by name under your holder
            var partTransform = mainHolder.Find(inst.partId);
            if (partTransform == null)
                continue;

            // 3) get the half‑size of that part in cell units
            var tv = partTransform.GetComponent<TrackPartView>();
            var sprite = tv.MainPartImage.sprite;
            Vector2 half = sprite.bounds.size * 0.5f;
            
            // 4) for each gridPt, recover the local‑space point and then world‑space
            foreach (var gp in spline)
            {
                // exactly the same centering math you used in DrawLocalSpline:
                var localPos = new Vector3(gp[0] - half.x,half.y - gp[1],-0.1f);
                worldPts.Add(partTransform.TransformPoint(localPos));
            }
        }

        // 5) feed into the global renderer
        globalPathRenderer.positionCount = worldPts.Count;
        globalPathRenderer.SetPositions(worldPts.ToArray());
    }
}

