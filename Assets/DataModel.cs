using System;
using System.Collections.Generic;

// Centralized class for all serializable level data
[Serializable]
public class DataModel
{
    [Serializable]
    public class PlacedPart
    {
        public string partName; // Name or ID to identify which TrackPart
        public int gridX, gridY; // Grid position
        public int rotation; // 0, 90, 180, 270
    }

    public List<PlacedPart> placedParts = new List<PlacedPart>();
}