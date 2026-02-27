using System;
using UnityEngine;

// Tracks hunger and handles all food-related behaviours.
//
// Blackboard stats written:
//   hunger  (0–100)
//
// Events consumed:
//   do_eat             → navigate to nearest open BurgerStore and eat there
//   do_cook            → cook at home (navigate home first if not already there)
//   do_seek_groceries  → navigate to nearest open Supermarket, then go home
//   arrived            → act on food-related arrivals (eat, cook, deposit groceries)
//
// Events raised:
//   move_to            → via LocomotionModule
//   do_go_home         → after eating out, to trigger journey home
public class FoodModule : IAgentModule
{
    // ── Config ─────────────────────────────────────────────────────────────────
    public float HungerRatePerSlowTick = 0.5f;  // ~100s to full hunger at default

    private const float CookDuration         = 5f;
    private const float CookHungerRestored   = 80f;
    private const float CookChildFeedAmount  = 64f;  // 80 % of CookHungerRestored for dependents
    private const float EatDuration          = 3f;

    // ── State ──────────────────────────────────────────────────────────────────
    private bool  isCooking = false;
    private float cookTimer = 0f;
    private bool  isEating  = false;
    private float eatTimer  = 0f;

    private BurgerStore  currentStore;
    private Supermarket  currentMarket;

    private Action<string, object> eventHandler;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        agent.SetStat("hunger", 0f);

        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_eat":            HandleDoEat(agent);           break;
                case "do_cook":           HandleDoCook(agent);          break;
                case "do_seek_groceries": HandleDoSeekGroceries(agent); break;
                case "arrived":           HandleArrived(agent, data);   break;
            }

            // If another task takes over while we are mid-travel, abandon our intent
            // so we don't act incorrectly when "arrived" eventually fires.
            if (evt.StartsWith("do_") && evt != "do_eat"
                                      && evt != "do_cook"
                                      && evt != "do_seek_groceries")
            {
                CancelTravelIntent(agent);
            }
        };

        agent.OnEvent += eventHandler;
    }

    public void Tick(AgentV2 agent)
    {
        if (isCooking)
        {
            cookTimer -= Time.deltaTime;
            // TODO: roll HealthManager.cookingInjuryChancePerSecond each frame
            if (cookTimer <= 0f) FinishCooking(agent);
        }

        if (isEating)
        {
            eatTimer -= Time.deltaTime;
            if (eatTimer <= 0f) FinishEating(agent);
        }
    }

    public void SlowTick(AgentV2 agent)
    {
        float rate = HungerRatePerSlowTick;
        if      (agent.Tags.Contains("life_baby"))    rate = 0.15f;
        else if (agent.Tags.Contains("life_toddler")) rate = 0.25f;

        agent.ModifyStat("hunger", rate, 0f, 100f);
    }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
    }

    // ── Public API (called by other modules, e.g. to feed dependents) ──────────
    public void RestoreHunger(AgentV2 agent, float amount)
    {
        agent.ModifyStat("hunger", -amount, 0f, 100f);
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoEat(AgentV2 agent)
    {
        if (agent.BuildingManager == null) return;

        var tile = agent.BuildingManager.FindNearest<BurgerStore>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position),
            b => b.IsOpen());

        if (tile == null) return;

        currentStore = agent.BuildingManager.GetBuildingAt(tile.Value) as BurgerStore;
        if (currentStore == null) return;

        agent.CurrentTask = "eat";
        agent.RaiseEvent("move_to", TileToWorld(agent, tile.Value));
    }

    private void HandleDoCook(AgentV2 agent)
    {
        if (agent.Tags.Contains("at_home"))
        {
            StartCooking(agent);
            return;
        }

        // Not home yet — travel there first; cooking starts on arrival.
        var homeModule = agent.GetModule<HomeModule>();
        if (homeModule == null || !homeModule.HasHome) return;

        agent.CurrentTask = "cook_travel";
        agent.RaiseEvent("move_to", homeModule.HomeWorldPosition);
    }

    private void HandleDoSeekGroceries(AgentV2 agent)
    {
        if (agent.BuildingManager == null) return;

        var tile = agent.BuildingManager.FindNearest<Supermarket>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position),
            b => b.IsOpen());

        if (tile == null) return;

        currentMarket = agent.BuildingManager.GetBuildingAt(tile.Value) as Supermarket;
        if (currentMarket == null) return;

        agent.CurrentTask = "groceries";
        agent.RaiseEvent("move_to", TileToWorld(agent, tile.Value));
    }

    private void HandleArrived(AgentV2 agent, object data)
    {
        switch (agent.CurrentTask)
        {
            case "eat":
                if (currentStore != null && currentStore.Interact(agent))
                    StartEating(agent);
                else
                    agent.CurrentTask = "";
                currentStore = null;
                break;

            case "cook_travel":
                // Arrived home — begin cooking.
                StartCooking(agent);
                break;

            case "groceries":
                currentMarket?.Interact(agent);
                currentMarket = null;
                agent.CurrentTask = "";
                agent.RaiseEvent("do_go_home");
                break;
        }
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask is "eat" or "cook_travel" or "groceries")
        {
            agent.CurrentTask = "";
            currentStore  = null;
            currentMarket = null;
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────────
    private void StartCooking(AgentV2 agent)
    {
        isCooking = true;
        cookTimer = CookDuration;
        agent.CurrentTask = "cook";
    }

    private void StartEating(AgentV2 agent)
    {
        isEating = true;
        eatTimer = EatDuration;
    }

    private void FinishCooking(AgentV2 agent)
    {
        isCooking = false;

        // Consume one unit of groceries from the home pantry.
        agent.GetModule<HomeModule>()?.ConsumePantryGroceries(1);

        // Feed self.
        RestoreHunger(agent, CookHungerRestored);

        // Feed dependents sharing the dwelling (Babies and Toddlers that need parent feeding).
        // TODO: iterate HomeModule.DwellingUnit.DwellingOccupancy and feed NeedsParentFeed agents
        //       with CookChildFeedAmount (= 80 % of CookHungerRestored).

        agent.CurrentTask = "";
    }

    private void FinishEating(AgentV2 agent)
    {
        isEating = false;
        agent.CurrentTask = "";

        // Head home after eating out.
        if (agent.GetModule<HomeModule>()?.HasHome == true)
            agent.RaiseEvent("do_go_home");
    }

    private static Vector3 TileToWorld(AgentV2 agent, Vector3Int tile)
    {
        return agent.BuildingsTilemap != null
            ? agent.BuildingsTilemap.CellToWorld(tile)
            : new Vector3(tile.x, tile.y, 0f);
    }
}
