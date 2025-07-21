using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Connection
{
    public string SourcePartId { get; set; } // ID of the source PlacedPartInstance
    public string TargetPartId { get; set; } // ID of the target PlacedPartInstance
    public PlacedPartInstance.ExitDetails SourceExit { get; set; } // Exit details from the source part
    public PlacedPartInstance.ExitDetails TargetExit { get; set; } // Exit details from the target part
    public AllowedPath AllowedPath { get; set; } // Allowed path that validates this connection

    public override string ToString()
    {
        return $"Connection: {SourcePartId} -> {TargetPartId}, Path: {AllowedPath?.entryConnectionId}-{AllowedPath?.exitConnectionId}";
    }
}

public class ConnectionBuilder
{
    // Builds a list of connections from PlacedPartInstances

    public static List<Connection> BuildConnections(List<PlacedPartInstance> instances)
    {
        var connections = new List<Connection>();
        var addedConnections = new HashSet<(string, string)>(); // Tracks added connections (sourceId, targetId)

        foreach (var sourceInstance in instances)
        {
            Debug.Log($"Source Instance: {sourceInstance.partId}, Position: {sourceInstance.position}");

            foreach (var sourceExit in sourceInstance.exits)
            {
                Debug.Log($"  Checking Source Exit: {sourceExit.exitIndex}, NeighborCell: {sourceExit.neighborCell}");

                // Find potential neighbor instances
                var neighborInstance = instances.FirstOrDefault(
                    neighbor => neighbor.position == sourceExit.neighborCell && neighbor.partId != sourceInstance.partId
                );

                if (neighborInstance != null)
                {
                    Debug.Log($"    Found Neighbor Instance: {neighborInstance.partId}, Position: {neighborInstance.position}");

                    // Find matching exit in the neighbor
                    var targetExit = neighborInstance.exits.FirstOrDefault(neighborExit =>
                        neighborExit.worldCell == sourceExit.neighborCell
                    );

                    if (targetExit != null)
                    {
                        Debug.Log($"      Found Target Exit: {targetExit.exitIndex}, WorldCell: {targetExit.worldCell}");

                        // Add connection in both directions
                        var forwardConnectionKey = (sourceInstance.partId, neighborInstance.partId);
                        var reverseConnectionKey = (neighborInstance.partId, sourceInstance.partId);

                        // Add forward connection
                        if (!addedConnections.Contains(forwardConnectionKey))
                        {
                            var connection = new Connection
                            {
                                SourcePartId = sourceInstance.partId,
                                TargetPartId = neighborInstance.partId,
                                SourceExit = sourceExit,
                                TargetExit = targetExit,
                                AllowedPath = null // Irrelevant for inter-part connections
                            };

                            connections.Add(connection);
                            addedConnections.Add(forwardConnectionKey);
                            Debug.Log($"        Added Forward Connection: {connection.SourcePartId} -> {connection.TargetPartId}");
                        }

                        // Add reverse connection
                        if (!addedConnections.Contains(reverseConnectionKey))
                        {
                            var reverseConnection = new Connection
                            {
                                SourcePartId = neighborInstance.partId,
                                TargetPartId = sourceInstance.partId,
                                SourceExit = targetExit,
                                TargetExit = sourceExit,
                                AllowedPath = null // Irrelevant for inter-part connections
                            };

                            connections.Add(reverseConnection);
                            addedConnections.Add(reverseConnectionKey);
                            Debug.Log($"        Added Reverse Connection: {reverseConnection.SourcePartId} -> {reverseConnection.TargetPartId}");
                        }
                    }
                    else
                    {
                        Debug.Log($"      No Matching Target Exit Found for Neighbor {neighborInstance.partId}");
                    }
                }
                else
                {
                    Debug.Log($"    No Neighbor Instance Found for NeighborCell {sourceExit.neighborCell}");
                }
            }
        }

        Debug.Log($"Total Connections Found: {connections.Count}");
        return connections;
    }
}

