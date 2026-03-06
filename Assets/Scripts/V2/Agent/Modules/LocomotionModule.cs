using System;
using System.Collections.Generic;
using UnityEngine;

// Handles all agent movement. Other modules request movement by raising a "move_to" event
// with a Vector3 destination. LocomotionModule fires "arrived" or "path_failed" in response.
//
// Pathfinding integration is marked TODO — wire up to the existing A* system here.
public class LocomotionModule : IAgentModule
{
    public float MoveSpeed = 2f;

    private Vector3 destination;
    private List<Vector3> path = new();
    private int pathIndex = 0;
    private bool isMoving = false;

    private Action<string, object> eventHandler;

    public void Initialize(AgentV2 agent)
    {
        eventHandler = (eventType, data) =>
        {
            if (eventType == "move_to" && data is Vector3 target)
                BeginMoveTo(agent, target);
        };

        agent.OnEvent += eventHandler;
    }

    private void BeginMoveTo(AgentV2 agent, Vector3 target)
    {
        destination = target;
        pathIndex   = 0;
        isMoving    = true;

        if (agent.Pathfinder != null && agent.BuildingsTilemap != null)
        {
            Vector3Int startCell = agent.BuildingsTilemap.WorldToCell(agent.transform.position);
            Vector3Int endCell   = agent.BuildingsTilemap.WorldToCell(target);

            var tilePath = agent.Pathfinder.FindPath(startCell, endCell);

            if (tilePath == null || tilePath.Count == 0)
            {
                isMoving = false;
                agent.RaiseEvent("path_failed", target);
                return;
            }

            path = new List<Vector3>(tilePath.Count);
            foreach (Vector3Int tile in tilePath)
                path.Add(agent.BuildingsTilemap.GetCellCenterWorld(tile));
        }
        else
        {
            // Fallback: move directly (no pathfinder available).
            path = new List<Vector3> { target };
        }
    }

    public void Tick(AgentV2 agent)
    {
        if (!isMoving || path.Count == 0) return;

        Vector3 nextWaypoint = path[pathIndex];
        float step = MoveSpeed * Time.deltaTime;

        agent.transform.position = Vector3.MoveTowards(
            agent.transform.position, nextWaypoint, step);

        if (Vector3.Distance(agent.transform.position, nextWaypoint) < 0.05f)
        {
            pathIndex++;

            if (pathIndex >= path.Count)
            {
                // Reached final destination.
                isMoving = false;
                path.Clear();
                agent.RaiseEvent("arrived", destination);
            }
        }
    }

    public void SlowTick(AgentV2 _) { }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
    }
}
