using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BuildingManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap buildingsTilemap;

    private Dictionary<Vector3Int, Building> buildings = new();

    public void RegisterBuilding(Building building)
    {
        buildings[building.gridPosition] = building;
    }

    public void DeregisterBuilding(Vector3Int pos)
    {
        buildings.Remove(pos);
    }

    public Building GetBuildingAt(Vector3Int pos)
    {
        buildings.TryGetValue(pos, out Building data);
        return data;
    }

    public Vector3Int? FindAvailableHome()
    {
        Debug.Log("Running find home check");
        foreach (var kvp in buildings)
        {
            if (kvp.Value is ResidentialBuilding residential && residential.HasVacantUnit())
                return kvp.Key;
        }
        return null;
    }

    // public Vector3Int? FindNearestBuildingOfType(Vector3Int from, string type)
    // {
    //     Vector3Int? nearest = null;
    //     float nearestDist = float.MaxValue;

    //     foreach (var kvp in buildings)
    //     {
    //         if (kvp.Value.BuildingType == type)
    //         {
    //             float dist = Vector3Int.Distance(from, kvp.Key);
    //             if (dist < nearestDist)
    //             {
    //                 nearestDist = dist;
    //                 nearest = kvp.Key;
    //             }
    //         }
    //     }

    //     return nearest;
    // }

    // // Overload that also checks capacity
    // public Vector3Int? FindNearestBuildingOfType(Vector3Int from, string type, bool requireCapacity)
    // {
    //     Vector3Int? nearest = null;
    //     float nearestDist = float.MaxValue;

    //     foreach (var kvp in buildings)
    //     {
    //         if (kvp.Value.BuildingType == type)
    //         {
    //             if (requireCapacity && !kvp.Value.HasCapacity)
    //                 continue;

    //             float dist = Vector3Int.Distance(from, kvp.Key);
    //             if (dist < nearestDist)
    //             {
    //                 nearestDist = dist;
    //                 nearest = kvp.Key;
    //             }
    //         }
    //     }

    //     return nearest;
    // }
}