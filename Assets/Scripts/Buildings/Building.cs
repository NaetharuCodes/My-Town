using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class Building : MonoBehaviour
{
    [Header("Building Info")]
    public string buildingName;
    public string buildingType;
    public Vector3Int gridPosition;

    [Header("Quality")]
    [Range(0f, 100f)]
    public float quality = 100f;
    public float degradationRate = 0.01f; // per game hour

    [Header("Pricing")]
    public int basePrice;

    [Header("Capacity")]
    public int maxOccupancy;

    // --- Allocated Roles (persistent, not presence-based) ---
    private Dictionary<string, List<Agent>> allocatedRoles = new();
    private Dictionary<string, int> roleCapacities = new();

    // --- Physical Presence (who is here right now) ---
    private HashSet<Agent> presentAgents = new();

    public int CurrentPresence => presentAgents.Count;
    public bool HasCapacity => presentAgents.Count < maxOccupancy;

    protected virtual void Update()
    {
        // Quality degrades over time for all buildings
        quality = Mathf.Max(quality - degradationRate * Time.deltaTime, 0f);
    }

    // --- The key method subclasses will override ---
    public virtual bool Interact(Agent agent)
    {
        // Base implementation does nothing
        // Subclasses define what happens when an agent interacts
        return false;
    }

    // --- Role Management (from BuildingData) ---
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

    // --- Pricing ---
    public virtual int GetCurrentPrice()
    {
        // Base implementation just returns the base price
        // Subclasses can override with market modifiers
        return basePrice;
    }
}