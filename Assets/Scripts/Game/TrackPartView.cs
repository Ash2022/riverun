﻿using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;


public class TrackPartView : MonoBehaviour
{
    [SerializeField] SpriteRenderer mainPartImage;
    [SerializeField]LineRenderer lineRenderer1;
    [SerializeField] LineRenderer lineRenderer2;


    public SpriteRenderer MainPartImage { get => mainPartImage; set => mainPartImage = value; }


    /// <summary>
    /// Called from LevelVisualizer.BuildCoroutine after Instantiate.
    /// </summary>
    public void Setup(PlacedPartInstance model,TrackPart trackPart)
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


        

        // 3) compute the “half‐size” of this part in LOCAL grid‐units
        //    sprite.bounds.size is (gridWidth, gridHeight) at scale==1
        Vector2 half = sprite.bounds.extents;

        if(trackPart.splineTemplates.Count == 1)
        {
            Destroy(lineRenderer2);
            DrawLocalSpline(trackPart.splineTemplates[0], half, lineRenderer1);
        }
        else
        {
            //there are 2 splines

            DrawLocalSpline(trackPart.splineTemplates[0], half, lineRenderer1);
            DrawLocalSpline(trackPart.splineTemplates[1], half, lineRenderer2);
        }

        // 4) draw each spline directly into local space
        /*
        foreach (var spline in trackPart.splineTemplates)
            DrawLocalSpline(spline, half);*/


        
    }

    private void DrawLocalSpline(List<float[]> spline, Vector2 half,LineRenderer lineRenderer)
    {
        // Make the LineRenderer interpret its positions in this transform’s local space
        lineRenderer.useWorldSpace = false;

        lineRenderer.positionCount = spline.Count;
        for (int i = 0; i < spline.Count; i++)
        {
            // center pt (0..W, 0..H) around (0,0):
            Vector3 local = new Vector3(
                spline[i][0] - half.x, half.y-spline[i][1],-0.05f);
            lineRenderer.SetPosition(i, local);
        }
    }
}
