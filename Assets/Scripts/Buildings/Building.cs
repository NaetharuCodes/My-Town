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

    // Revenue collected from rent or sales. In the full version this will
    // pay wages to employees and then flow up to the owner (chain, private
    // individual, or municipality).
    [Header("Finances")]
    public int treasury = 0;

    [Header("Capacity")]
    public int maxOccupancy;

    [Header("Fire")]
    public bool isOnFire = false;
    public float fireChancePerHour = 0.002f; // chance to ignite each game-hour (0 = fireproof)
    public float fireDamageRate = 8f;        // quality lost per real second while burning

    // Lazy-resolved so Building needs no Start() wiring in subclasses.
    private TimeManager _fireTimeManager;
    private BuildingManager _fireBuildingManager;

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

        if (isOnFire)
        {
            quality = Mathf.Max(quality - fireDamageRate * Time.deltaTime, 0f);
            if (quality <= 0f)
            {
                isOnFire = false;
                Debug.Log($"{buildingName} at {gridPosition} has burnt down!");
            }
        }
        else if (fireChancePerHour > 0f)
        {
            if (_fireTimeManager == null) _fireTimeManager = FindFirstObjectByType<TimeManager>();
            if (_fireBuildingManager == null) _fireBuildingManager = FindFirstObjectByType<BuildingManager>();

            if (_fireTimeManager != null)
            {
                float chancePerSecond = fireChancePerHour / _fireTimeManager.realSecondsPerGameHour;
                if (Random.value < chancePerSecond * Time.deltaTime)
                    CatchFire();
            }
        }
    }

    void CatchFire()
    {
        isOnFire = true;
        Debug.Log($"{buildingName} at {gridPosition} is on fire!");

        if (_fireBuildingManager != null)
        {
            Vector3Int? stationPos = _fireBuildingManager.FindNearest<FireStation>(gridPosition);
            if (stationPos.HasValue)
            {
                FireStation station = (FireStation)_fireBuildingManager.GetBuildingAt(stationPos.Value);
                station.DispatchFirefighter(this);
            }
            else
                Debug.Log($"No fire station available to respond to fire at {buildingName}!");
        }
    }

    public void Extinguish()
    {
        isOnFire = false;
        Debug.Log($"Fire at {buildingName} ({gridPosition}) has been extinguished.");
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