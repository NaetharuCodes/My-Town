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

    public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<Vector3Int, Building>> GetAllBuildings()
        => buildings;

    public void ClearAll()
    {
        buildings.Clear();
    }

    public Vector3Int? FindNearest<T>(Vector3Int from, System.Func<T, bool> filter = null) where T : Building
    {
        Vector3Int? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var kvp in buildings)
        {
            if (kvp.Value is T building)
            {
                if (filter != null && !filter(building)) continue;

                float dist = Vector3Int.Distance(from, kvp.Key);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = kvp.Key;
                }
            }
        }

        return nearest;
    }
}