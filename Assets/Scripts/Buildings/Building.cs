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

    // --- Physical Presence (who is here right now) ---
    private HashSet<AgentV2> presentV2Agents = new();

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
                EventLog.LogDanger($"{buildingName} has burnt down!");
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
        EventLog.LogDanger($"{buildingName} is on fire!");
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
            {
                EventLog.LogWarning($"No fire station to respond to fire at {buildingName}!");
                Debug.Log($"No fire station available to respond to fire at {buildingName}!");
            }
        }
    }

    public void Extinguish()
    {
        isOnFire = false;
        EventLog.LogWarning($"Fire at {buildingName} extinguished.");
        Debug.Log($"Fire at {buildingName} ({gridPosition}) has been extinguished.");
    }

    // --- Physical Presence ---
    public void EnterV2(AgentV2 agent)
    {
        if (agent != null) presentV2Agents.Add(agent);
    }

    public void ExitV2(AgentV2 agent)
    {
        if (agent != null) presentV2Agents.Remove(agent);
    }

    public IReadOnlyCollection<AgentV2> GetPresentV2Agents() => presentV2Agents;

    protected virtual void OnDestroy()
    {
        presentV2Agents.Clear();
    }

    // --- Pricing ---
    public virtual int GetCurrentPrice()
    {
        // Base implementation just returns the base price
        // Subclasses can override with market modifiers
        return basePrice;
    }
}