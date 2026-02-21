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

    public IEnumerable<Vector3Int> FindAvailableHomes()
    {
        foreach (var kvp in buildings)
        {
            if (kvp.Value is ResidentialBuilding residential && residential.HasVacantUnit())
                yield return kvp.Key;
        }
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

    // Returns all buildings of type T matching the optional filter, sorted nearest-first.
    public IEnumerable<Vector3Int> FindAllSorted<T>(Vector3Int from, System.Func<T, bool> filter = null) where T : Building
    {
        var results = new List<(float dist, Vector3Int pos)>();

        foreach (var kvp in buildings)
        {
            if (kvp.Value is T building)
            {
                if (filter != null && !filter(building)) continue;
                results.Add((Vector3Int.Distance(from, kvp.Key), kvp.Key));
            }
        }

        results.Sort((a, b) => a.dist.CompareTo(b.dist));

        foreach (var entry in results)
            yield return entry.pos;
    }
}