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

        AssignRandomPersonality(agent);
        agents.Add(agent);
    }
    public IReadOnlyList<Agent> GetAllAgents() => agents;

    public void DespawnAll()
    {
        foreach (Agent agent in agents)
            if (agent != null) Destroy(agent.gameObject);
        agents.Clear();
    }

    // Spawns an agent from saved data: position and name are restored;
    // needs/finances are applied after; home/job links are resolved in a second pass.
    public Agent SpawnAgentFromSave(AgentSaveData data)
    {
        Vector3 worldPos = new Vector3(data.worldX, data.worldY, 0f);
        GameObject agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity);
        Agent agent = agentObj.GetComponent<Agent>();
        agent.agentName = data.agentName;
        agent.Initialise(this, FindFirstObjectByType<Pathfinder>(), buildingsTilemap, buildingManager);
        agents.Add(agent);
        return agent;
    }

    void AssignRandomPersonality(Agent agent)
    {
        // ~20% chance of being a thief; level 1 (25% steal chance) or level 2 (50% steal chance).
        if (Random.value < 0.2f)
        {
            int level = Random.Range(1, 3);
            agent.personality.SetTrait("thief", level);
            Debug.Log($"{agent.agentName} has personality trait: thief:{level}");
        }
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