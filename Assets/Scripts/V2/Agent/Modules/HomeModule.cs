using System;
using UnityEngine;

// Manages home state and handles all housing-related behaviour.
//
// Blackboard tags written:
//   has_home   → agent has an assigned dwelling
//   at_home    → agent is currently at their home tile
//
// Blackboard stats written:
//   pantry_groceries  → units of groceries currently in the home pantry
//
// Events consumed:
//   do_go_home    → navigate to the home tile
//   do_seek_home  → find a vacant dwelling and claim it
//   arrived       → deposit carried groceries and mark at_home
//
// Events raised:
//   move_to       → via LocomotionModule
//
// Public API (used by FoodModule, SchoolModule, SocialModule, building demolition):
//   HasHome, HomeWorldPosition
//   AssignHome(tile, dwelling), ClearHome()
//   AddCarriedGroceries(n), ConsumePantryGroceries(n)
//   DwellingUnit   (exposed for FoodModule to feed dependents)
public class HomeModule : IAgentModule
{
    private const float AtHomeRadius   = 0.6f;   // world-units; close enough to count as "home"
    private const float HomelessLeaveTime = 120f; // seconds homeless before agent leaves town

    // ── Home state ─────────────────────────────────────────────────────────────
    public bool         HasHome          { get; private set; } = false;
    public Vector3      HomeWorldPosition { get; private set; }
    public DwellingUnit DwellingUnit     { get; private set; }

    private Vector3Int  homeTile;
    private int         carriedGroceries = 0;
    private float       homelessTimer    = 0f;

    private Action<string, object> eventHandler;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        agent.Tags.Remove("has_home");
        agent.Tags.Remove("at_home");
        agent.SetStat("pantry_groceries", 0f);

        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_go_home":   HandleDoGoHome(agent);     break;
                case "do_seek_home": HandleDoSeekHome(agent);   break;
                case "arrived":      HandleArrived(agent, data); break;
            }
        };
        agent.OnEvent += eventHandler;
    }

    public void Tick(AgentV2 agent)
    {
        // Update at_home tag based on proximity.
        bool near = HasHome
            && Vector3.Distance(agent.transform.position, HomeWorldPosition) <= AtHomeRadius;

        if (near) agent.Tags.Add("at_home");
        else      agent.Tags.Remove("at_home");

        // Homeless timer — agent leaves town if they can't find shelter.
        if (!HasHome)
        {
            homelessTimer += Time.deltaTime;
            if (homelessTimer >= HomelessLeaveTime)
            {
                // TODO: AgentManager.Instance.RemoveAgent(agent) — wire up when V2 managers exist
                Debug.Log($"{agent.Name}: homeless too long — leaving town");
                UnityEngine.Object.Destroy(agent.gameObject);
            }
        }
        else
        {
            homelessTimer = 0f;
        }
    }

    public void SlowTick(AgentV2 _) { }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    public void AssignHome(AgentV2 agent, Vector3Int tile, DwellingUnit unit)
    {
        homeTile          = tile;
        HomeWorldPosition = agent.BuildingsTilemap != null
            ? agent.BuildingsTilemap.CellToWorld(tile)
            : new Vector3(tile.x, tile.y, 0f);
        DwellingUnit      = unit;
        HasHome           = true;
        homelessTimer     = 0f;

        agent.Tags.Add("has_home");
        agent.SetStat("pantry_groceries", unit?.pantry?.Get(ItemType.Groceries) ?? 0f);
    }

    public void ClearHome(AgentV2 agent)
    {
        DwellingUnit?.DwellingOccupancyV2.Remove(agent);
        DwellingUnit      = null;
        HasHome           = false;
        HomeWorldPosition = default;

        agent.Tags.Remove("has_home");
        agent.Tags.Remove("at_home");
        agent.SetStat("pantry_groceries", 0f);

        if (agent.CurrentTask is "home" or "home_seek")
            agent.CurrentTask = "";
    }

    public void AddCarriedGroceries(int amount)    => carriedGroceries += amount;
    public void ConsumePantryGroceries(int amount)
    {
        if (DwellingUnit == null) return;
        DwellingUnit.pantry.Remove(ItemType.Groceries, amount);
        // Stat stays in sync via HandleArrived refresh; also decrement immediately.
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoGoHome(AgentV2 agent)
    {
        if (!HasHome) return;
        if (agent.Tags.Contains("at_home")) return; // Already home.

        agent.CurrentTask = "home";
        agent.RaiseEvent("move_to", HomeWorldPosition);
    }

    private void HandleDoSeekHome(AgentV2 agent)
    {
        if (HasHome || agent.BuildingManager == null) return;

        agent.CurrentTask = "home_seek";

        // ── Stage 1: family unification ────────────────────────────────────────
        // TODO: if agent has a family, check if any member already has a home and join them.
        // Requires FamilyModule or access to family data — wire up when available.

        // ── Stage 2: find best vacant unit ────────────────────────────────────
        int neededBedrooms = 1; // TODO: use family.members.Count when FamilyModule exists

        foreach (var homeTilePos in agent.BuildingManager.FindAvailableHomes())
        {
            var building = agent.BuildingManager.GetBuildingAt(homeTilePos) as ResidentialBuilding;
            if (building == null) continue;

            var unit = building.FindBestVacantUnit(neededBedrooms);
            if (unit == null) continue;

            // Claim the unit.
            unit.DwellingOccupancyV2.Add(agent);
            AssignHome(agent, homeTilePos, unit);

            agent.CurrentTask = "home";
            agent.RaiseEvent("move_to", HomeWorldPosition);
            return;
        }

        // No home found this cycle — return to idle and retry on next DecisionModule tick.
        agent.CurrentTask = "";
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "home") return;

        agent.CurrentTask = "";
        agent.Tags.Add("at_home");

        // Deposit any groceries carried from the supermarket into the pantry.
        if (carriedGroceries > 0 && DwellingUnit != null)
        {
            DwellingUnit.pantry.Add(ItemType.Groceries, carriedGroceries);
            carriedGroceries = 0;
        }

        // Refresh pantry stat from the actual dwelling inventory.
        if (DwellingUnit != null)
            agent.SetStat("pantry_groceries", DwellingUnit.pantry.Get(ItemType.Groceries));
    }
}
