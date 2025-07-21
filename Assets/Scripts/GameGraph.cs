using System.Collections.Generic;
using UnityEngine;
using static GraphBuilder;
using static PlacedPartInstance;

public class GraphBuilder
{
    // Method to build the graph using both PlacedPartInstances (for nodes) and Connections (for edges)
    public GameGraph BuildGraph(List<PlacedPartInstance> placedPartInstances, List<Connection> connections)
    {
        GameGraph graph = new GameGraph();

        graph.nodes.Clear();

        // Step 1: Add nodes from PlacedPartInstances
        foreach (var instance in placedPartInstances)
        {
            bool isSimpleNode = instance.exits.Count == 2;

            var graphNode = new GraphNode(instance.partId, isSimpleNode);

            // For complex nodes, store AllowedPathGroups and exits
            if (!isSimpleNode)
            {
                graphNode.allowedPathsGroup = instance.allowedPathsGroup;
                graphNode.exits = instance.exits;
            }

            graph.nodes[instance.partId] = graphNode;
        }

        // Step 2: Add connections (edges) between nodes
        foreach (var connection in connections)
        {
            if (graph.nodes.ContainsKey(connection.SourcePartId) && graph.nodes.ContainsKey(connection.TargetPartId))
            {
                graph.nodes[connection.SourcePartId].connections.Add(connection.TargetPartId);
                graph.nodes[connection.TargetPartId].connections.Add(connection.SourcePartId); // Assuming bidirectional connections
            }
            else
            {
                Debug.LogWarning($"Connection involves unknown nodes: {connection.SourcePartId} -> {connection.TargetPartId}");
            }
        }

        return graph;
    }

    // Method to retrieve neighbors of a node by partId
    public static List<string> GetNeighbors(string partId, GameGraph graph)
    {
        if (!graph.nodes.ContainsKey(partId))
        {
            Debug.Log($"Part with ID {partId} does not exist in the graph.");
            return new List<string>();
        }

        return graph.nodes[partId].connections; // Return directly from the connections list
    }

    public class GraphNode
    {
        public string partId; // ID of the part
        public bool isSimpleNode; // True if the node has exactly two exits
        public List<AllowedPathGroup> allowedPathsGroup; // Only for complex nodes
        public List<ExitDetails> exits; // Use the existing Exit Details from PlacedPartInstance
        public List<string> connections; // List of connected node IDs

        public GraphNode(string partId, bool isSimpleNode)
        {
            this.partId = partId;
            this.isSimpleNode = isSimpleNode;
            this.allowedPathsGroup = isSimpleNode ? null : new List<AllowedPathGroup>();
            this.exits = isSimpleNode ? null : new List<ExitDetails>();
            this.connections = new List<string>();
        }
    }

    public class GameGraph
    {
        public Dictionary<string, GraphNode> nodes = new Dictionary<string, GraphNode>();
    }
}