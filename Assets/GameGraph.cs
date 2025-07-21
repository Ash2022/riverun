using System.Collections.Generic;
using UnityEngine;

// GraphModel to represent the graph structure
public class GraphModel
{
    public Dictionary<string, List<string>> AdjacencyList { get; private set; }

    public GraphModel()
    {
        AdjacencyList = new Dictionary<string, List<string>>();
    }

    public void AddEdge(string source, string target)
    {
        if (!AdjacencyList.ContainsKey(source))
            AdjacencyList[source] = new List<string>();

        if (!AdjacencyList.ContainsKey(target))
            AdjacencyList[target] = new List<string>();

        if (!AdjacencyList[source].Contains(target))
            AdjacencyList[source].Add(target);

        if (!AdjacencyList[target].Contains(source))
            AdjacencyList[target].Add(source);
    }

    public List<string> GetNeighbors(string partId)
    {
        if (AdjacencyList.ContainsKey(partId))
            return AdjacencyList[partId];
        return new List<string>();
    }

    public bool AreConnected(string partA, string partB)
    {
        if (AdjacencyList.ContainsKey(partA))
            return AdjacencyList[partA].Contains(partB);
        return false;
    }

    public void PrintGraph()
    {
        Debug.Log("Printing graph...");
        foreach (var node in AdjacencyList)
        {
            Debug.Log($"{node.Key}: {string.Join(", ", node.Value)}");
        }
    }
}

public class GameGraph
{
    public GraphModel BuildGraph(List<Connection> connections)
    {
        Debug.Log("Starting to build the graph...");
        var graphModel = new GraphModel();

        foreach (var connection in connections)
        {
            Debug.Log($"Processing connection: {connection.SourcePartId} ↔ {connection.TargetPartId}");

            // Add bi-directional edges to the graph model
            graphModel.AddEdge(connection.SourcePartId, connection.TargetPartId);

            Debug.Log($"Added bi-directional edge: {connection.SourcePartId} ↔ {connection.TargetPartId}");
        }

        Debug.Log($"Graph Built with {graphModel.AdjacencyList.Count} Nodes");
        return graphModel;
    }
}