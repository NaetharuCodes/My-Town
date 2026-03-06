using System.Collections.Generic;
using UnityEngine;

// Manages the periodic arrival of new families into the town.
// Families arrive at an ArrivalPoint building (bus stop / ferry port) on a schedule.
//
// Future: replace arrivalChance with a city attractiveness score driven by wealth,
// services quality, housing availability, crime rate, etc.
// TODO: replace arrivalChance with city attractiveness score
public class ArrivalManager : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public AgentManager agentManager;
    public BuildingManager buildingManager;

    [Header("Arrival Settings")]
    [Tooltip("Chance (0–1) that any families arrive at each transport departure (every 4 game-hours).")]
    public float arrivalChance = 0.65f;

    [Tooltip("Maximum number of families that can arrive on a single transport.")]
    public int maxFamiliesPerTransport = 40;

    [Tooltip("No new families will arrive once the population reaches this cap.")]
    public int maxPopulation = 50000;

    [Tooltip("No new families arrive while this many residents are already homeless.")]
    public int maxHomelessBeforeStop = 50;

    private TimeManager timeManager;

    // Hours at which a transport departs (every 4 hours).
    private static readonly int[] DepartureHours = { 6, 10, 14, 18, 22, 2 };

    void Start()
    {
        if (agentManager == null) agentManager = FindFirstObjectByType<AgentManager>();
        if (buildingManager == null) buildingManager = FindFirstObjectByType<BuildingManager>();

        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
            timeManager.OnHourChanged += OnHourChanged;

        if (agentManager == null)
            Debug.LogError("ArrivalManager: AgentManager not found in scene.");
        if (buildingManager == null)
            Debug.LogError("ArrivalManager: BuildingManager not found in scene.");
    }

    // Right-click this component in the Inspector → "Test: Spawn Family Now" to force an immediate arrival.
    [ContextMenu("Test: Spawn Family Now")]
    public void TestSpawnFamily() => SpawnTransport();

    void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnHourChanged -= OnHourChanged;
    }

    void OnHourChanged(int hour)
    {
        foreach (int dep in DepartureHours)
        {
            if (hour != dep) continue;

            int pop = agentManager.GetAllAgentsV2().Count;
            if (pop >= maxPopulation) return;
            if (Random.value < arrivalChance)
                SpawnTransport();

            return; // only one departure slot per hour
        }
    }

    void SpawnTransport()
    {
        int familyCount = Random.Range(1, maxFamiliesPerTransport + 1);
        int totalPeople = 0;

        for (int i = 0; i < familyCount; i++)
        {
            int pop = agentManager.GetAllAgentsV2().Count;
            if (pop + totalPeople >= maxPopulation) break;
            totalPeople += SpawnFamily();
        }

        if (totalPeople > 0)
            EventLog.Log($"Transport arrived — {totalPeople} new resident{(totalPeople == 1 ? "" : "s")} in town.");
    }

    // Returns the number of people spawned, or 0 if the arrival point is missing or no housing is available.
    int SpawnFamily()
    {
        // Find the arrival point building.
        Vector3Int? arrivalPos = buildingManager.FindNearest<ArrivalPoint>(Vector3Int.zero);
        if (!arrivalPos.HasValue)
        {
            Debug.LogWarning("ArrivalManager: No ArrivalPoint building found on the map. " +
                             "Place a Bus Stop or Ferry Port to enable family arrivals.");
            return 0;
        }
        Vector3Int spawnTile = arrivalPos.Value;

        // Roll a family type and build the member list before spawning anyone.
        FamilyType type = RollFamilyType();
        string lastName = agentManager.GenerateLastName();
        List<(LifeStage stage, FamilyRole role)> members = BuildMemberList(type);

        // Don't spawn more arrivals if too many residents are already homeless.
        if (CountHomeless() >= maxHomelessBeforeStop)
        {
            Debug.Log("ArrivalManager: Too many homeless residents — transport passed through without stopping.");
            return 0;
        }

        Family family = new Family(lastName, type);

        // Spawn all members at the arrival tile.
        foreach (var (stage, role) in members)
            agentManager.SpawnFamilyMemberV2(spawnTile, family, stage, role, lastName);

        agentManager.RegisterFamily(family);

        string desc = FamilyDescription(type, members.Count);
        Debug.Log($"ArrivalManager: {desc} arrived — the {lastName} family ({members.Count} members).");
        return members.Count;
    }

    // Counts how many bedrooms a family described by the member list needs.
    static int CalcBedroomsNeeded(List<(LifeStage stage, FamilyRole role)> members)
    {
        int adults = 0, children = 0;
        foreach (var m in members)
        {
            if (m.role == FamilyRole.Child) children++;
            else adults++;
        }
        return adults + Mathf.CeilToInt(children / 2f);
    }

    // Counts agents currently without a home.
    int CountHomeless()
    {
        int count = 0;
        foreach (AgentV2 a in agentManager.GetAllAgentsV2())
            if (a.GetModule<HomeModule>()?.HasHome != true) count++;
        return count;
    }

    // -------------------------------------------------------------------------

    static FamilyType RollFamilyType()
    {
        float roll = Random.value;
        if (roll < 0.20f) return FamilyType.Solo;
        if (roll < 0.40f) return FamilyType.YoungCouple;
        if (roll < 0.65f) return FamilyType.SmallFamily;
        if (roll < 0.80f) return FamilyType.LargeFamily;
        if (roll < 0.90f) return FamilyType.RetiredCouple;
        return FamilyType.SingleParent;
    }

    static List<(LifeStage, FamilyRole)> BuildMemberList(FamilyType type)
    {
        var list = new List<(LifeStage, FamilyRole)>();

        switch (type)
        {
            case FamilyType.Solo:
                list.Add((LifeStage.Adult, FamilyRole.Head));
                break;

            case FamilyType.YoungCouple:
                list.Add((LifeStage.Adult, FamilyRole.Head));
                list.Add((LifeStage.Adult, FamilyRole.Partner));
                break;

            case FamilyType.SmallFamily:
                list.Add((LifeStage.Adult, FamilyRole.Head));
                list.Add((LifeStage.Adult, FamilyRole.Partner));
                int smallKids = Random.Range(1, 3);
                for (int i = 0; i < smallKids; i++)
                    list.Add((RollChildStage(), FamilyRole.Child));
                break;

            case FamilyType.LargeFamily:
                list.Add((LifeStage.Adult, FamilyRole.Head));
                list.Add((LifeStage.Adult, FamilyRole.Partner));
                int largeKids = Random.Range(3, 6);
                for (int i = 0; i < largeKids; i++)
                    list.Add((RollChildStage(), FamilyRole.Child));
                break;

            case FamilyType.RetiredCouple:
                list.Add((LifeStage.Elder, FamilyRole.Head));
                list.Add((LifeStage.Elder, FamilyRole.Partner));
                break;

            case FamilyType.SingleParent:
                list.Add((LifeStage.Adult, FamilyRole.Head));
                int spKids = Random.Range(1, 4);
                for (int i = 0; i < spKids; i++)
                    list.Add((RollChildStage(), FamilyRole.Child));
                break;
        }

        return list;
    }

    static LifeStage RollChildStage()
    {
        float r = Random.value;
        if (r < 0.15f) return LifeStage.Baby;
        if (r < 0.30f) return LifeStage.Toddler;
        if (r < 0.55f) return LifeStage.YoungChild;
        if (r < 0.80f) return LifeStage.OlderChild;
        return LifeStage.Teen;
    }

    static string FamilyDescription(FamilyType type, int count)
    {
        return type switch
        {
            FamilyType.Solo => "a solo arrival",
            FamilyType.YoungCouple => "a young couple",
            FamilyType.SmallFamily => $"a small family of {count}",
            FamilyType.LargeFamily => $"a large family of {count}",
            FamilyType.RetiredCouple => "a retired couple",
            FamilyType.SingleParent => $"a single-parent family of {count}",
            _ => $"a family of {count}"
        };
    }
}
