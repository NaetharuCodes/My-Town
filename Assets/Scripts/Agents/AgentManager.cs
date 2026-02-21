using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class AgentManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap buildingsTilemap;
    public BuildingManager buildingManager;

    [Header("Agent Settings")]
    public GameObject agentPrefab;

    private List<Agent> agents = new List<Agent>();

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector3Int cellPos = buildingsTilemap.WorldToCell(worldPos);
            SpawnAgent(cellPos);
        }
    }

    public void SpawnAgent(Vector3Int spawnTile)
    {
        Vector3 worldPos = buildingsTilemap.CellToWorld(spawnTile)
                           + new Vector3(0.5f, 0.5f, 0f);

        GameObject agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity);
        Agent agent = agentObj.GetComponent<Agent>();
        agent.agentName = GenerateName();
        agent.Initialise(this, FindFirstObjectByType<Pathfinder>(), buildingsTilemap, buildingManager);

        Vector3Int? home = buildingManager.FindAvailableHome();
        if (home.HasValue)
        {
            Building building = buildingManager.GetBuildingAt(home.Value);
            if (building is ResidentialBuilding residential)
            {
                residential.Interact(agent);
            }
        }
        else
        {
            Debug.Log($"{agent.agentName} couldn't find a home — homeless!");
        }

        agents.Add(agent);
    }
    string GenerateName()
    {
        string[] firstNames = { "James", "Emma", "Oliver", "Sophia", "Liam",
                                "Ava", "Noah", "Mia", "Jack", "Lily" };
        string[] lastNames = { "Smith", "Chen", "Patel", "Williams", "Brown",
                               "Jones", "Garcia", "Miller", "Davis", "Wilson" };

        return $"{firstNames[Random.Range(0, firstNames.Length)]} " +
               $"{lastNames[Random.Range(0, lastNames.Length)]}";
    }
}