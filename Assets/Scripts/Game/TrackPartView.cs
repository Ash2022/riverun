using UnityEngine;


public class TrackPartView : MonoBehaviour
{
    [SerializeField] SpriteRenderer mainPartImage;

    public SpriteRenderer MainPartImage { get => mainPartImage; set => mainPartImage = value; }


    /// <summary>
    /// Called from LevelVisualizer.BuildCoroutine after Instantiate.
    /// </summary>
    public void Setup(PlacedPartInstance model)
    {
        // 1) pick the correct sprite
        var sprite = LevelVisualizer.Instance.GetSpriteFor(model.partType);
        if (sprite != null)
            mainPartImage.sprite = sprite;
        else
            Debug.Log($"No sprite for partType '{model.partType}'");

        // 2) size so that 1 grid-cell = CellSize world units
        //    our sprites import at 100px = 1 unit, and a 2×1 part is 200×100 px → 2×1 world units.
        float s = LevelVisualizer.Instance.CellSize;
        transform.localScale = new Vector3(s, s, 1f);
    }
}
