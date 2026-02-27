using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AgentManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap         buildingsTilemap;
    public BuildingManager buildingManager;
    public TimeManager     timeManager;

    [Header("Agent Settings (V1)")]
    public GameObject agentPrefab;

    [Header("Agent Settings (V2)")]
    public GameObject agentV2Prefab;

    private List<Agent>   agents   = new List<Agent>();
    private List<AgentV2> agentsV2 = new List<AgentV2>();
    public  List<Family>  families = new List<Family>();

    public IReadOnlyList<Agent>   GetAllAgents()   => agents;
    public IReadOnlyList<AgentV2> GetAllAgentsV2() => agentsV2;

    private void Start()
    {
        if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>();
    }

    // Spawns a family member at the given tile, wires up family/role/life stage, and names them.
    public Agent SpawnFamilyMember(Vector3Int spawnTile, Family family, LifeStage stage,
                                   FamilyRole role, string familyLastName)
    {
        Vector3 worldPos = buildingsTilemap.CellToWorld(spawnTile) + new Vector3(0.5f, 0.5f, 0f);

        GameObject agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity);
        Agent agent = agentObj.GetComponent<Agent>();

        agent.agentName = $"{GenerateFirstName()} {familyLastName}";
        agent.familyRole = role;
        agent.family = family;
        family.AddMember(agent);

        int age = DefaultAgeFor(stage);
        agent.Initialise(this, FindFirstObjectByType<Pathfinder>(), buildingsTilemap, buildingManager, stage, age);

        AssignRandomPersonality(agent);
        agents.Add(agent);
        return agent;
    }

    // V2 equivalent — instantiates an AgentV2 prefab and wires up the full module stack.
    public AgentV2 SpawnFamilyMemberV2(Vector3Int spawnTile, Family family, LifeStage stage,
                                       FamilyRole role, string familyLastName)
    {
        Vector3 worldPos = buildingsTilemap.CellToWorld(spawnTile) + new Vector3(0.5f, 0.5f, 0f);

        GameObject agentObj = Instantiate(agentV2Prefab, worldPos, Quaternion.identity);
        AgentV2 agent = agentObj.GetComponent<AgentV2>();

        agent.Name = $"{GenerateFirstName()} {familyLastName}";

        // Scene deps must be wired up before any module Initialize() runs.
        agent.Initialise(buildingManager, FindFirstObjectByType<Pathfinder>(), buildingsTilemap, timeManager);

        // Starting balance — modules check this stat immediately.
        agent.SetStat("bank_balance", Random.Range(200f, 500f));

        // Module stack — LifeStageModule first so capability tags are set for all subsequent modules.
        int age = DefaultAgeFor(stage);
        agent.AddModule(new LifeStageModule(stage, age));
        agent.AddModule(new HomeModule());
        agent.AddModule(new WorkModule());
        agent.AddModule(new SchoolModule());
        agent.AddModule(new FoodModule());
        agent.AddModule(new MedicalModule());
        agent.AddModule(new SocialModule());
        agent.AddModule(new LocomotionModule());
        agent.AddModule(new DecisionModule());  // last — starts making decisions immediately

        agentsV2.Add(agent);
        return agent;
    }

    public void RemoveAgentV2(AgentV2 agent)
    {
        agentsV2.Remove(agent);
    }

    public void RegisterFamily(Family family)
    {
        families.Add(family);
    }

    public void RemoveAgent(Agent agent)
    {
        agents.Remove(agent);
    }

    public void DespawnAll()
    {
        foreach (Agent agent in agents)
            if (agent != null) Destroy(agent.gameObject);
        agents.Clear();
        families.Clear();
    }

    // Spawns an agent from saved data: position and name are restored;
    // needs/finances are applied after; home/job links are resolved in a second pass.
    public Agent SpawnAgentFromSave(AgentSaveData data)
    {
        Vector3 worldPos = new Vector3(data.worldX, data.worldY, 0f);
        GameObject agentObj = Instantiate(agentPrefab, worldPos, Quaternion.identity);
        Agent agent = agentObj.GetComponent<Agent>();
        agent.agentName = data.agentName;

        LifeStage stage = System.Enum.TryParse(data.lifeStage, out LifeStage parsed) ? parsed : LifeStage.Adult;
        agent.Initialise(this, FindFirstObjectByType<Pathfinder>(), buildingsTilemap, buildingManager,
                         stage, data.ageInYears);

        if (System.Enum.TryParse(data.familyRole, out FamilyRole roleP))
            agent.familyRole = roleP;

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
            EventLog.LogWarning($"{agent.agentName} has a thief streak (level {level}).");
            Debug.Log($"{agent.agentName} has personality trait: thief:{level}");
        }
    }

    string GenerateFirstName()
    {
        string[] names = { "James", "Emma", "Oliver", "Sophia", "Liam",
                           "Ava", "Noah", "Mia", "Jack", "Lily",
                           "Ethan", "Chloe", "Lucas", "Grace", "Mason",
                           "Ella", "Aiden", "Zoe", "Leo", "Ruby" };
        return names[Random.Range(0, names.Length)];
    }

    public string GenerateLastName()
    {
        string[] names = { "Smith", "Chen", "Patel", "Williams", "Brown",
                           "Jones", "Garcia", "Miller", "Davis", "Wilson",
                           "Taylor", "Anderson", "Thomas", "Jackson", "White" };
        return names[Random.Range(0, names.Length)];
    }

    static int DefaultAgeFor(LifeStage stage)
    {
        return stage switch
        {
            LifeStage.Baby           => Random.Range(0, 2),
            LifeStage.Toddler        => Random.Range(2, 5),
            LifeStage.YoungChild     => Random.Range(5, 10),
            LifeStage.OlderChild     => Random.Range(10, 13),
            LifeStage.Teen           => Random.Range(13, 18),
            LifeStage.Adult          => Random.Range(20, 60),
            LifeStage.Elder          => Random.Range(65, 80),
            LifeStage.VenerableElder => Random.Range(80, 101),
            _                        => 25
        };
    }
}
