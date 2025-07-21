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
        var checkedConnections = new HashSet<(string, string)>(); // Tracks checked connections (sourceId, targetId)

        foreach (var sourceInstance in instances)
        {
            Debug.Log($"Source Instance: {sourceInstance.partId}, Position: {sourceInstance.position}");

            foreach (var sourceExit in sourceInstance.exits)
            {
                Debug.Log($"  Checking Source Exit: {sourceExit.exitIndex}, NeighborCell: {sourceExit.neighborCell}");

                var neighborInstance = instances.FirstOrDefault(
                    neighbor => neighbor.position == sourceExit.neighborCell && neighbor.partId != sourceInstance.partId
                );

                if (neighborInstance != null)
                {
                    Debug.Log($"    Found Neighbor Instance: {neighborInstance.partId}, Position: {neighborInstance.position}");

                    var targetExit = neighborInstance.exits.FirstOrDefault(neighborExit =>
                        neighborExit.worldCell == sourceExit.neighborCell
                    );

                    if (targetExit != null)
                    {
                        Debug.Log($"      Found Target Exit: {targetExit.exitIndex}, WorldCell: {targetExit.worldCell}");

                        // Create a unique key for the connection (sorted to avoid duplicates)
                        var connectionKey = (sourceInstance.partId, neighborInstance.partId);
                        var reverseConnectionKey = (neighborInstance.partId, sourceInstance.partId);

                        if (!checkedConnections.Contains(connectionKey) && !checkedConnections.Contains(reverseConnectionKey))
                        {
                            // Add forward connection
                            var connection = new Connection
                            {
                                SourcePartId = sourceInstance.partId,
                                TargetPartId = neighborInstance.partId,
                                SourceExit = sourceExit,
                                TargetExit = targetExit,
                                AllowedPath = null
                            };
                            connections.Add(connection);

                            // Add reverse connection implicitly by marking both directions as checked
                            checkedConnections.Add(connectionKey);
                            checkedConnections.Add(reverseConnectionKey);

                            Debug.Log($"        Added Bi-Directional Connection: {connection.SourcePartId} ↔ {connection.TargetPartId}");
                        }
                        else
                        {
                            Debug.Log($"        Skipped Duplicate Connection: {connectionKey.Item1} ↔ {connectionKey.Item2}");
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

