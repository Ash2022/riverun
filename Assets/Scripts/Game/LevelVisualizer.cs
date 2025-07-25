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
    public float CellSize { get => cellSize; set => cellSize = value; }

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
            level = JsonUtility.FromJson<LevelData>(levelJson.text);
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

        Vector2 worldOrigin;
        {
            // use fixed logical frame size
            float sizeX = frameWidthUnits / gridW;
            float sizeY = frameHeightUnits / gridH;

            // pick the larger so we fill (and potentially overflow) one axis
            cellSize = Mathf.Max(sizeX, sizeY);

            // compute grid's half‑size in world units
            Vector2 halfGrid = new Vector2(gridW, gridH) * cellSize * 0.5f;

            // assume the frame's center == this.transform.position
            Vector2 frameCenter = (Vector2)transform.position;

            // origin is bottom‑left of grid: center minus halfGrid, plus half a cell
            worldOrigin = frameCenter - halfGrid + Vector2.one * (cellSize * 0.5f);
        }

        foreach (var inst in level.parts)
        {
            // compute average of all occupied cells in grid‑space
            Vector2 sum = Vector2.zero;
            foreach (var c in inst.occupyingCells)
                sum += new Vector2(c.x - minX + 0.5f, c.y - minY + 0.5f);
            Vector2 avg = sum / inst.occupyingCells.Count;

            // flip Y so (0,0) bottom‑left
            Vector2 flipped = new Vector2(avg.x, gridH - avg.y);

            // world position
            Vector3 pos = new Vector3(
                worldOrigin.x + flipped.x * cellSize,
                worldOrigin.y + flipped.y * cellSize,
                0f
            );

            // spawn
            var go = Instantiate(partPrefab, mainHolder);
            go.name = inst.partId;
            go.transform.position = pos;
            // rotate around Z by inst.rotation degrees clockwise
            go.transform.rotation = Quaternion.Euler(0f, 0f, -inst.rotation);

            // hand off to view
            if (go.TryGetComponent<TrackPartView>(out var view))
                view.Setup(inst);


           


            yield return new WaitForSeconds(tileDelay);
        }
    }

    /// <summary>  
    /// Returns the sprite for the given partType, or null if not found.  
    /// </summary>
    public Sprite GetSpriteFor(string partType)
    {
        int idx = partsLibrary.FindIndex(p => p.partName == partType);
        return (idx >= 0 && idx < partSprites.Count) ? partSprites[idx] : null;
    }
}
