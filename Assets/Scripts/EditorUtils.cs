
using UnityEditor;
using UnityEngine;

namespace EditorUtils
{
    /// <summary>Small, editor-only helpers to draw game points on the grid.</summary>
    public static class GuiDrawHelpers
    {
        // ---------- Grid math ----------

        public static Vector2 CellCenter(Rect gridRect, float cellSize, int gx, int gy)
        {
            return new Vector2(
                gridRect.x + gx * cellSize + cellSize * 0.5f,
                gridRect.y + gy * cellSize + cellSize * 0.5f
            );
        }

        public static Rect CellRect(Rect gridRect, float cellSize, int gx, int gy, int w = 1, int h = 1)
        {
            return new Rect(
                gridRect.x + gx * cellSize,
                gridRect.y + gy * cellSize,
                w * cellSize,
                h * cellSize
            );
        }

        public static Rect CellRect(Rect gridRect, float cellSize,int gx, int gy,float wCells, float hCells)
        {
            float x = gridRect.x + gx * cellSize;
            float y = gridRect.y + gy * cellSize;
            return new Rect(x, y, wCells * cellSize, hCells * cellSize);
        }

        // ---------- Drawing primitives ----------

        public static void DrawStationDisc(Vector2 center, float radius, Color fill, Color outline)
        {
            Handles.color = fill;
            Handles.DrawSolidDisc(center, Vector3.forward, radius);
            Handles.color = outline;
            Handles.DrawWireDisc(center, Vector3.forward, radius);
        }

        /// <summary>Draws a 3×1 train rectangle aligned to the grid (horizontal). Pass vertical=true to rotate 1×3.</summary>
        public static Rect CellRectCentered(Rect gridRect, float cellSize,
                                        int gx, int gy,
                                        float wCells, float hCells)
        {
            float cx = gridRect.x + gx * cellSize + cellSize * 0.5f;
            float cy = gridRect.y + gy * cellSize + cellSize * 0.5f;
            float w = wCells * cellSize;
            float h = hCells * cellSize;
            return new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
        }

        public static Rect TrainRectFromHead(Rect gridRect, float cellSize,
                                         int gx, int gy, TrainDir dir,
                                         float lengthCells = 3f, float thickCells = 0.35f)
        {
            // head world center
            float cx = gridRect.x + gx * cellSize + cellSize * 0.5f;
            float cy = gridRect.y + gy * cellSize + cellSize * 0.5f;

            float L = lengthCells * cellSize;
            float T = thickCells * cellSize;

            switch (dir)
            {
                case TrainDir.Up:    // head at clicked cell, body extends upward
                    return new Rect(cx - T * 0.5f, cy, T, L);
                case TrainDir.Down:  // extends downward
                    return new Rect(cx - T * 0.5f, cy - L, T, L);
                case TrainDir.Right: // extends right
                    return new Rect(cx - L, cy - T * 0.5f, L, T);
                case TrainDir.Left:  // extends left
                    return new Rect(cx, cy - T * 0.5f, L, T);
                default:
                    return Rect.zero;
            }
        }

        public static void DrawTrainRect(Rect r, Color fill, Color outline, int segments = 3)
        {
            // face
            EditorGUI.DrawRect(r, fill);
            // outline
            Handles.color = outline;
            Handles.DrawLine(new Vector3(r.x, r.y), new Vector3(r.xMax, r.y));
            Handles.DrawLine(new Vector3(r.xMax, r.y), new Vector3(r.xMax, r.yMax));
            Handles.DrawLine(new Vector3(r.xMax, r.yMax), new Vector3(r.x, r.yMax));
            Handles.DrawLine(new Vector3(r.x, r.yMax), new Vector3(r.x, r.y));

            // segments
            float step = r.width > r.height ? r.width / segments : r.height / segments;
            for (int i = 1; i < segments; i++)
            {
                if (r.width > r.height)
                {
                    float x = r.x + step * i;
                    Handles.DrawLine(new Vector3(x, r.y), new Vector3(x, r.yMax));
                }
                else
                {
                    float y = r.y + step * i;
                    Handles.DrawLine(new Vector3(r.x, y), new Vector3(r.xMax, y));
                }
            }
        }

        /// <summary>Draw a simple depot polygon (house-like pentagon).</summary>
        public static void DrawDepotPoly(Rect r, Color fill, Color outline)
        {
            // House shape: bottom rectangle + roof triangle
            Vector3 p0 = new Vector3(r.x,      r.yMax);
            Vector3 p1 = new Vector3(r.xMax,   r.yMax);
            Vector3 p2 = new Vector3(r.xMax,   r.y + r.height * 0.4f);
            Vector3 p3 = new Vector3(r.x + r.width * 0.5f, r.y);
            Vector3 p4 = new Vector3(r.x,      r.y + r.height * 0.4f);

            var poly = new[] { p0, p1, p2, p3, p4 };

            Handles.DrawAAConvexPolygon(poly);
            Handles.DrawSolidRectangleWithOutline(Rect.zero, Color.clear, Color.clear); // force outline next
            Handles.color = outline;
            for (int i = 0; i < poly.Length; i++)
                Handles.DrawLine(poly[i], poly[(i + 1) % poly.Length]);

            // Fill hack (since DrawAAConvexPolygon uses current color)
            Handles.color = fill;
            Handles.DrawAAConvexPolygon(poly);
        }

        // ---------- Labels ----------

        public static void DrawCenteredLabel(Vector2 center, string text, int fontSize = 12, Color? color = null)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize
            };
            if (color.HasValue) style.normal.textColor = Color.black;

            Vector2 size = style.CalcSize(new GUIContent(text));
            GUI.Label(new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y), text, style);
        }

        // Convenience overload if you already have a rect
        public static void DrawCenteredLabel(Rect rect, string text, int fontSize = 14, Color? color = null)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize
            };
            if (color.HasValue) style.normal.textColor = Color.black;

            GUI.Label(rect, text, style);
        }
    }
}

