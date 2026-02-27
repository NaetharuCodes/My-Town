// TestSpawner.cs
// Attach to the same GameObject as AgentV2.
// Drag in the scene singletons via the Inspector, then hit Play.
using UnityEngine;
using UnityEngine.Tilemaps;

public class TestSpawner : MonoBehaviour
{
    [Header("Scene references (drag in via Inspector)")]
    public BuildingManager buildingManager;
    public Pathfinder      pathfinder;
    public Tilemap         buildingsTilemap;
    public TimeManager     timeManager;

    [Header("Agent config")]
    public string    agentName  = "Test Agent";
    public LifeStage lifeStage  = LifeStage.Adult;
    public int       ageInYears = 30;

    [Header("Starting stats")]
    [Tooltip("Starting bank balance — min/max of a random range")]
    public float startingMoneyMin = 200f;
    public float startingMoneyMax = 500f;

    private void Start()
    {
        var agent = GetComponent<AgentV2>();

        // 1. Wire up scene dependencies before any module Initialise() runs.
        agent.Initialise(buildingManager, pathfinder, buildingsTilemap, timeManager);

        // 2. Identity.
        agent.Name = agentName;

        // 3. Starting stats that modules depend on (set before AddModule so
        //    any module that reads them in Initialize sees valid values).
        agent.SetStat("bank_balance", Random.Range(startingMoneyMin, startingMoneyMax));

        // 4. Modules — order: life-stage tags first so all other modules
        //    can read capability tags in their Initialize if needed.
        agent.AddModule(new LifeStageModule(lifeStage, ageInYears));
        agent.AddModule(new HomeModule());
        agent.AddModule(new WorkModule());
        agent.AddModule(new SchoolModule());
        agent.AddModule(new FoodModule());
        agent.AddModule(new MedicalModule());
        agent.AddModule(new SocialModule());
        agent.AddModule(new LocomotionModule());
        agent.AddModule(new DecisionModule());   // last — starts firing decisions
    }
}
