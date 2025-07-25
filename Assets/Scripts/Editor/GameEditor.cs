﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-side manager for placing/cycling/removing GamePoints.
/// </summary>
public class GameEditor
{
    private readonly GameModel _data;
    private readonly CellOccupationManager _cellMgr;
    private readonly int _colorCount;

    public GameEditor(GameModel gameData, CellOccupationManager cellMgr, int colorCount = 3)
    {
        _data = gameData;
        _cellMgr = cellMgr;
        _colorCount = colorCount;
    }

    public List<GamePoint> GetPoints() => _data.points;
    public void SetPoints(List<GamePoint> pts) => _data.points = pts;

    /// <summary>
    /// Handle a click on the grid in “game” mode.
    /// mouseButton: 0=LMB add/cycle color, 1=RMB cycle type, 2=MMB delete
    /// </summary>
    public void OnGridCellClicked(PlacedPartInstance clickedPart,
                                  int gx, int gy,
                                  int mouseButton,
                                  GamePointType selectedType,
                                  int colorIndex = 0)
    {
        var point = _data.points.FirstOrDefault(p => p.gridX == gx && p.gridY == gy);

        if (mouseButton == 0) // Left click
        {
            if (point == null)
            {
                // Add new
                var anchor = BuildAnchor(clickedPart, gx, gy);
                _data.points.Add(new GamePoint(clickedPart, gx, gy, selectedType, colorIndex, anchor));
            }
            else
            {
                // Cycle color
                point.colorIndex = (point.colorIndex + 1) % _colorCount;
            }
        }
        else if (mouseButton == 1) // Right click - cycle type
        {
            if (point != null)
            {
                point.type = NextType(point.type);
            }
        }
        else if (mouseButton == 2) // Middle click - delete
        {
            if (point != null)
            {
                _data.points.Remove(point);
            }
        }
    }

    private GamePointType NextType(GamePointType current)
    {
        return current switch
        {
            GamePointType.Station => GamePointType.Depot,
            GamePointType.Depot => GamePointType.Train,
            GamePointType.Train => GamePointType.Station,
            _ => GamePointType.Station
        };
    }

    /// <summary>
    /// Build an Anchor from the clicked cell and part.
    /// For now: choose the closest exit pin on the part (or -1 if none).
    /// </summary>
    private Anchor BuildAnchor(PlacedPartInstance part, int gx, int gy)
    {
        if (part == null || part.exits == null || part.exits.Count == 0)
            return new Anchor { partId = part?.partId ?? "none", exitPin = -1, splineIndex = -1, t = 0f };

        // Find nearest exit pin by Manhattan or Euclidean distance
        var clickCell = new UnityEngine.Vector2Int(gx, gy);
        int bestPin = part.exits[0].exitIndex;
        float bestDist = float.PositiveInfinity;

        foreach (var ex in part.exits)
        {
            float d = UnityEngine.Vector2Int.Distance(clickCell, ex.worldCell);
            if (d < bestDist)
            {
                bestDist = d;
                bestPin = ex.exitIndex;
            }
        }

        return Anchor.FromPin(part.partId, bestPin);
    }

    public void ClearAll(bool resetIds = true)
    {
        _data.points.Clear();
        if (resetIds) GamePoint.ResetIds();
    }


    public void DrawStationsUI(Rect gridRect, List<GamePoint> points, CellOccupationManager cellManager, Color[] colors, float cellSize)
    {
        // Panels go to the right of the grid, stacked vertically.
        const float panelW = 180f;
        const float stationH = 60f;
        //const float trainH = 42f;
        const float personSize = 24f;
        const float spacing = 12f;

        float y = gridRect.y; // running y offset

        // Only stations & trains (you can add Depot later if you want it here too)
        foreach (var p in points.Where(pt => pt.type == GamePointType.Station || pt.type == GamePointType.Train))
        {
            // Resolve cell/part info
            Vector2Int cell = new Vector2Int(p.gridX, p.gridY);
            string partId = "none";
            if (cellManager != null && cellManager.cellToPart.TryGetValue(cell, out PlacedPartInstance partInst))
                partId = partInst.partId;

            if (p.type == GamePointType.Station)
            {
                Rect box = new Rect(gridRect.xMax + spacing, y, panelW, stationH);

                // Header
                GUI.Label(new Rect(box.x, box.y, box.width, 18f),
                          $"Station {p.id} | Cell {cell} | Part: {partId}",
                          new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });

                // Waiting people
                for (int j = 0; j < p.waitingPeople.Count; j++)
                {
                    int colorIdx = p.waitingPeople[j];
                    Rect pr = new Rect(box.x + j * (personSize + 5f), box.y + 22f, personSize, personSize);

                    EditorGUI.DrawRect(pr, colors[colorIdx % colors.Length]);
                    Handles.color = Color.black;
                    Handles.DrawSolidRectangleWithOutline(pr, Color.clear, Color.black);

                    // Click to cycle color
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                        pr.Contains(Event.current.mousePosition))
                    {
                        p.waitingPeople[j] = (colorIdx + 1) % colors.Length;
                        Event.current.Use();
                        //Repaint();
                    }
                }

                // Add person button
                Rect addBtn = new Rect(box.x, box.yMax - 22f, 80f, 18f);
                if (GUI.Button(addBtn, "Add Person"))
                {
                    p.waitingPeople.Add(0);
                    //Repaint();
                }

                y += stationH + spacing;
            }
            else // Train
            {
                // --- sizes (all UI pixels, not grid) ---
                float rowH = 18f;
                float cartSize = cellSize / 3f;
                float cartRowY = 42f;   // where the cart row starts (relative to box.y)
                float addBtnH = 18f;
                float spacingY = 6f;

                // Dynamic panel height (header + dir/color + carts + button)
                float trainH = cartRowY + cartSize + spacingY + addBtnH + 4f;

                Rect box = new Rect(gridRect.xMax + spacing, y, panelW, trainH);

                // Header
                GUI.Label(new Rect(box.x, box.y, box.width, rowH),
                          $"Train {p.id} | Cell {cell} | Part: {partId}",
                          new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold });

                // Direction button
                Rect dirBtn = new Rect(box.x, box.y + rowH + 2f, 70f, rowH);
                int dir = (int)p.direction;
                string arrow = dir switch { 0 => "↑", 1 => "→", 2 => "↓", 3 => "←", _ => "?" };
                if (GUI.Button(dirBtn, $"Dir {arrow}"))
                {
                    dir = (dir + 1) % 4;
                    p.direction = (TrainDir)dir;
                    //Repaint();
                }

                // Color cycle
                Rect colBtn = new Rect(dirBtn.xMax + 6f, dirBtn.y, 60f, rowH);
                if (GUI.Button(colBtn, "Color"))
                {
                    p.colorIndex = (p.colorIndex + 1) % colors.Length;
                    //Repaint();
                }

                // --- carts row ---
                // Ensure list exists
                if (p.initialCarts == null) p.initialCarts = new List<int>();

                for (int j = 0; j < p.initialCarts.Count; j++)
                {
                    int cIdx = p.initialCarts[j];
                    Rect cartRect = new Rect(box.x + j * (cartSize + 4f), box.y + cartRowY, cartSize, cartSize);

                    EditorGUI.DrawRect(cartRect, colors[cIdx % colors.Length]);
                    Handles.color = Color.black;
                    Handles.DrawSolidRectangleWithOutline(cartRect, Color.clear, Color.black);

                    if (Event.current.type == EventType.MouseDown && cartRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0)        // left: cycle color
                            p.initialCarts[j] = (cIdx + 1) % colors.Length;
                        else if (Event.current.button == 1)   // right: remove
                            p.initialCarts.RemoveAt(j);

                        Event.current.Use();
                        //Repaint();
                        break; // list changed
                    }
                }

                // Add Cart button
                Rect addBtn = new Rect(box.x, box.y + cartRowY + cartSize + spacingY, 80f, addBtnH);
                if (GUI.Button(addBtn, "Add Cart"))
                {
                    p.initialCarts.Add(0);
                    //Repaint();
                }

                y += trainH + spacing;
            }
        }
    }


    public void DrawGamePoints(Rect gridRect,float cellSize, Color[] colors)
    {
        foreach (var p in GetPoints())
        {
            Color col = colors[p.colorIndex % colors.Length];

            switch (p.type)
            {
                case GamePointType.Station:
                    {
                        Vector2 c = EditorUtils.GuiDrawHelpers.CellCenter(gridRect, cellSize, p.gridX, p.gridY);
                        EditorUtils.GuiDrawHelpers.DrawStationDisc(c, cellSize * 0.35f, col, Color.black);
                        EditorUtils.GuiDrawHelpers.DrawCenteredLabel(c, $"S_{p.id}", 12, Color.black);
                        break;
                    }

                case GamePointType.Train:
                    {
                        Color outline = Color.black;
                        Color headCol = colors[p.colorIndex % colors.Length];

                        // ---- sizes in cells ----
                        const float HEAD_LEN = 1.25f;
                        const float THICKNESS = 0.35f;
                        const float CART_LEN = 0.35f;
                        const float GAP_FRAC = 0.10f;  // 10% gap

                        // ---- pixels ----
                        float headLenPx = HEAD_LEN * cellSize;
                        float thickPx = THICKNESS * cellSize;
                        float cartLenPx = CART_LEN * cellSize;
                        float gapPx = cartLenPx * GAP_FRAC;

                        // anchor: cell center = train FRONT
                        Vector2 cc = EditorUtils.GuiDrawHelpers.CellCenter(gridRect, cellSize, p.gridX, p.gridY);

                        // build head rect so FRONT edge sits on cc, body extends “behind”
                        Rect headRect;
                        Vector2 baseStep;    // basic offset per cart (before gap)
                        bool vertical;

                        switch (p.direction)
                        {
                            case TrainDir.Up:
                                headRect = new Rect(cc.x - thickPx * 0.5f, cc.y, thickPx, headLenPx);
                                baseStep = new Vector2(0f, cartLenPx + gapPx);
                                vertical = true;
                                break;
                            case TrainDir.Right:
                                headRect = new Rect(cc.x - headLenPx, cc.y - thickPx * 0.5f, headLenPx, thickPx);
                                baseStep = new Vector2(-(cartLenPx + gapPx), 0f);
                                vertical = false;
                                break;
                            case TrainDir.Down:
                                headRect = new Rect(cc.x - thickPx * 0.5f, cc.y - headLenPx, thickPx, headLenPx);
                                baseStep = new Vector2(0f, -(cartLenPx + gapPx));
                                vertical = true;
                                break;
                            case TrainDir.Left:
                                headRect = new Rect(cc.x, cc.y - thickPx * 0.5f, headLenPx, thickPx);
                                baseStep = new Vector2(cartLenPx + gapPx, 0f);
                                vertical = false;
                                break;
                            default:
                                headRect = new Rect(cc.x - headLenPx * 0.5f, cc.y - thickPx * 0.5f, headLenPx, thickPx);
                                baseStep = Vector2.zero;
                                vertical = false;
                                break;
                        }

                        // draw head
                        EditorUtils.GuiDrawHelpers.DrawTrainRect(headRect, headCol, outline);

                        // ---- draw carts ----
                        // compute cart dims
                        float cartW = vertical ? thickPx : cartLenPx;
                        float cartH = vertical ? cartLenPx : thickPx;

                        // align carts on thickness axis
                        float alignDX = (headRect.width - cartW) * 0.5f;
                        float alignDY = (headRect.height - cartH) * 0.5f;

                        // find tail anchor (first cart's top-left corner)
                        float tailX, tailY;
                        switch (p.direction)
                        {
                            case TrainDir.Up:
                                tailX = headRect.x + alignDX;
                                tailY = headRect.yMax + gapPx * 0.5f;
                                break;
                            case TrainDir.Right:
                                tailX = headRect.x - cartLenPx - gapPx * 0.5f;
                                tailY = headRect.y + alignDY;
                                break;
                            case TrainDir.Down:
                                tailX = headRect.x + alignDX;
                                tailY = headRect.y - cartLenPx - gapPx * 0.5f;
                                break;
                            case TrainDir.Left:
                                tailX = headRect.xMax + gapPx * 0.5f;
                                tailY = headRect.y + alignDY;
                                break;
                            default:
                                tailX = headRect.x + alignDX;
                                tailY = headRect.yMax + gapPx * 0.5f;
                                break;
                        }

                        // draw each cart
                        for (int ci = 0; ci < p.initialCarts.Count; ci++)
                        {
                            Color cartCol = colors[p.initialCarts[ci] % colors.Length];

                            Rect cartRect = new Rect(
                                tailX + baseStep.x * ci,
                                tailY + baseStep.y * ci,
                                cartW,
                                cartH
                            );
                            EditorUtils.GuiDrawHelpers.DrawTrainRect(cartRect, cartCol, outline, 1);
                        }

                        // caption with direction arrow
                        string arrow = p.direction switch
                        {
                            TrainDir.Up => "↑",
                            TrainDir.Right => "→",
                            TrainDir.Down => "↓",
                            TrainDir.Left => "←",
                            _ => "?"
                        };
                        EditorUtils.GuiDrawHelpers.DrawCenteredLabel(headRect, $"T_{p.id} {arrow}", 12, Color.black);
                        break;
                    }


                case GamePointType.Depot:
                    {
                        Rect r = EditorUtils.GuiDrawHelpers.CellRectCentered(gridRect, cellSize, p.gridX, p.gridY,
                                                                             1.0f, 1.0f);
                        EditorUtils.GuiDrawHelpers.DrawDepotPoly(r, col, Color.black);
                        EditorUtils.GuiDrawHelpers.DrawCenteredLabel(r, $"D_{p.id}");
                        break;
                    }
            }
        }
    }
}
