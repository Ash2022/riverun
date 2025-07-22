using System;
using System.Collections.Generic;
using UnityEngine;

public class GraphModel
{
    // ---- Public containers ----
    public readonly Dictionary<NodeId, GraphNode> Nodes = new();
    public readonly Dictionary<string, List<GraphNode>> PartToNodes = new();

    // ---- Value types / nodes / edges ----
    public readonly struct NodeId : IEquatable<NodeId>
    {
        public readonly string partId;
        public readonly int exitIndex;

        public NodeId(string partId, int exitIndex)
        {
            this.partId = partId;
            this.exitIndex = exitIndex;
        }

        public bool Equals(NodeId other) => partId == other.partId && exitIndex == other.exitIndex;
        public override bool Equals(object obj) => obj is NodeId other && Equals(other);
        public override int GetHashCode() => (partId, exitIndex).GetHashCode();
        public override string ToString() => $"{partId}:{exitIndex}";
    }

    public class GraphNode
    {
        public NodeId Id;
        public PlacedPartInstance Part;
        public PlacedPartInstance.ExitDetails Exit;
        public List<GraphEdge> Edges = new();
    }

    public class GraphEdge
    {
        public GraphNode From;
        public GraphNode To;
        public float Cost;
    }
}
