using System.Collections.Generic;
using UnityEngine;
public class BuildingData
{
    public Vector3Int Position { get; private set; }
    public string BuildingType { get; private set; } // "House", "BurgerStore", "Cinema", etc.

    // --- Allocated Roles (persistent, not presence-based) ---
    // Each role has a max count and a list of agents filling it
    private Dictionary<string, List<Agent>> allocatedRoles = new();
    private Dictionary<string, int> roleCapacities = new();

    // --- Physical Presence (who is here right now) ---
    private HashSet<Agent> presentAgents = new();
    public int MaxOccupancy { get; private set; }

    public BuildingData(Vector3Int position, string buildingType, int maxOccupancy)
    {
        Position = position;
        BuildingType = buildingType;
        MaxOccupancy = maxOccupancy;
    }

    // --- Role Management ---
    public void DefineRole(string roleName, int maxCount)
    {
        roleCapacities[roleName] = maxCount;
        if (!allocatedRoles.ContainsKey(roleName))
            allocatedRoles[roleName] = new List<Agent>();
    }

    public bool HasRoleVacancy(string roleName)
    {
        if (!roleCapacities.ContainsKey(roleName)) return false;
        if (!allocatedRoles.ContainsKey(roleName)) return true;
        return allocatedRoles[roleName].Count < roleCapacities[roleName];
    }

    public bool AllocateRole(string roleName, Agent agent)
    {
        if (!HasRoleVacancy(roleName)) return false;
        allocatedRoles[roleName].Add(agent);
        return true;
    }

    public void DeallocateRole(string roleName, Agent agent)
    {
        if (allocatedRoles.ContainsKey(roleName))
            allocatedRoles[roleName].Remove(agent);
    }

    public List<Agent> GetAgentsInRole(string roleName)
    {
        return allocatedRoles.ContainsKey(roleName)
            ? allocatedRoles[roleName]
            : new List<Agent>();
    }

    // --- Physical Presence ---
    public bool HasCapacity => presentAgents.Count < MaxOccupancy;
    public int CurrentPresence => presentAgents.Count;

    public bool Enter(Agent agent)
    {
        if (!HasCapacity) return false;
        presentAgents.Add(agent);
        return true;
    }

    public void Exit(Agent agent)
    {
        presentAgents.Remove(agent);
    }
}