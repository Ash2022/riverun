using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static GraphBuilder;

public class PathStep
{
    public string PartId { get; set; } // ID of the part
    public int SplineIndex { get; set; } // Index of the spline path used

    public PathStep(string partId, int splineIndex)
    {
        PartId = partId;
        SplineIndex = splineIndex;
    }

    public override string ToString()
    {
        return $"PartId: {PartId}, SplineIndex: {SplineIndex}";
    }
}

public class PathFinder
{
    private GameGraph graphModel;

    // Empty constructor
    public PathFinder()
    {
        graphModel = null; // Initialize graphModel as null
    }

    // Constructor to initialize the graph model
    public PathFinder(GameGraph graphModel)
    {
        this.graphModel = graphModel;
    }

    // Method to initialize the PathFinder with a GraphModel
    public void InitPathFinder(GameGraph graph)
    {
        Debug.Log("Initializing PathFinder with a new graph...");
        this.graphModel = graph;
    }

    // Overload to find the shortest path between two PlacedPartInstance objects
    public List<PathStep> FindShortestPath(PlacedPartInstance sourceInstance, PlacedPartInstance targetInstance)
    {
        var startNode = graphModel.nodes[sourceInstance.partId];
        var endNode = graphModel.nodes[targetInstance.partId];

        // Use the normal FindShortestPath method with node IDs
        return FindShortestPath(startNode.partId, endNode.partId);
    }

    // Main method: Finds the shortest path between two graph nodes
    public List<PathStep> FindShortestPath(string startNode, string endNode)
    {
        if (graphModel == null)
        {
            Debug.LogError("GraphModel is not initialized. Use a valid graph model.");
            return new List<PathStep>(); // Return empty path if graph is not initialized
        }

        Debug.Log($"Finding shortest path from {startNode} to {endNode}...");

        var queue = new Queue<List<PathStep>>();
        var visited = new HashSet<string>();

        // Start with the initial node
        queue.Enqueue(new List<PathStep> { new PathStep(startNode, 0) });
        visited.Add(startNode);

        while (queue.Count > 0)
        {
            var path = queue.Dequeue();
            var currentStep = path[path.Count - 1]; // Last step in the path

            // If we've reached the target node
            if (currentStep.PartId == endNode)
            {
                Debug.Log($"Path found: {string.Join(" -> ", path)}");
                return path;
            }

            var currentNode = graphModel.nodes[currentStep.PartId]; // Accessing node correctly via dictionary

            // Explore neighbors based on node type
            foreach (var neighborId in  GraphBuilder.GetNeighbors(currentNode.partId, graphModel))
            {
                if (!visited.Contains(neighborId))
                {
                    visited.Add(neighborId);

                    // Retrieve neighbor node using dictionary key
                    var neighborNode = graphModel.nodes[neighborId]; // Accessing dictionary properly

                    int splineIndex = 0; // Default for simple nodes
                    if (!currentNode.isSimpleNode || !neighborNode.isSimpleNode)
                    {
                        // Calculate splineIndex for complex nodes
                        splineIndex = FindSplineIndex(currentNode, neighborNode);
                        if (splineIndex == -1) continue; // Skip invalid connections
                    }

                    var newPath = new List<PathStep>(path)
                    {
                        new PathStep(neighborId, splineIndex)
                    };
                    queue.Enqueue(newPath);
                }
            }
        }

        Debug.Log("No path found.");
        return new List<PathStep>(); // Return an empty path if no path is found
    }

    // Helper method to determine the spline index for complex nodes
    private int FindSplineIndex(GraphNode sourceNode, GraphNode targetNode)
    {
        Debug.Log($"Determining spline index for connection {sourceNode.partId} -> {targetNode.partId}");

        foreach (var allowedPathGroup in sourceNode.allowedPathsGroup)
        {
            foreach (var allowedPath in allowedPathGroup.allowedPaths)
            {
                foreach (var sourceExit in sourceNode.exits) // Using ExitDetails directly from PlacedPartInstance
                {
                    foreach (var targetExit in targetNode.exits) // Using ExitDetails directly from PlacedPartInstance
                    {
                        if (allowedPath.entryConnectionId == sourceExit.exitIndex &&
                            allowedPath.exitConnectionId == targetExit.exitIndex)
                        {
                            return sourceNode.allowedPathsGroup.IndexOf(allowedPathGroup); // Return the group index as the spline index
                        }
                    }
                }
            }
        }

        return -1; // Invalid spline index
    }
}