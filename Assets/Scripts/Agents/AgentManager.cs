using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AgentManager : MonoBehaviour
{
    [Header("References")]
    public Tilemap         buildingsTilemap;
    public BuildingManager buildingManager;
    public TimeManager     timeManager;

    [Header("Agent Settings")]
    public GameObject agentV2Prefab;

    private List<AgentV2> agentsV2 = new List<AgentV2>();
    public  List<Family>  families = new List<Family>();

    public IReadOnlyList<AgentV2> GetAllAgentsV2() => agentsV2;

    private void Start()
    {
        if (timeManager == null) timeManager = FindFirstObjectByType<TimeManager>();
    }

    public AgentV2 SpawnFamilyMemberV2(Vector3Int spawnTile, Family family, LifeStage stage,
                                       FamilyRole role, string familyLastName)
    {
        Vector3 worldPos = buildingsTilemap.CellToWorld(spawnTile) + new Vector3(0.5f, 0.5f, 0f);

        if (agentV2Prefab == null)
        {
            Debug.LogError("AgentManager: agentV2Prefab is not assigned in the Inspector!");
            return null;
        }

        GameObject agentObj = Instantiate(agentV2Prefab, worldPos, Quaternion.identity);
        AgentV2 agent = agentObj.GetComponent<AgentV2>();

        if (agent == null)
        {
            Debug.LogError($"AgentManager: prefab '{agentV2Prefab.name}' is missing the AgentV2 script component!");
            Destroy(agentObj);
            return null;
        }

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
        agent.AddModule(new SleepModule());
        agent.AddModule(new LocomotionModule());
        agent.AddModule(new DecisionModule());  // last — starts making decisions immediately

        agentsV2.Add(agent);
        family.AddMemberV2(agent);
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
