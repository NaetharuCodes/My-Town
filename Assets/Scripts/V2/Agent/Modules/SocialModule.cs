using System;
using UnityEngine;

// Tracks loneliness and handles park-visit socialisation behaviour.
//
// Blackboard stats written:
//   loneliness  (0–100)
//
// Events consumed:
//   do_socialise  → navigate to nearest Park and visit for visitDuration seconds
//   arrived       → start the visit timer on arrival at park
//
// Events raised:
//   move_to      → via LocomotionModule
//   do_go_home   → after the park visit ends
public class SocialModule : IAgentModule
{
    // ── Config ─────────────────────────────────────────────────────────────────
    public float LonelinessRatePerSlowTick = 0.2f;
    public float VisitDuration             = 10f;
    public float LonelinessRestored        = 70f;

    // ── State ──────────────────────────────────────────────────────────────────
    private bool  isVisiting  = false;
    private float visitTimer  = 0f;

    private Action<string, object> eventHandler;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        agent.SetStat("loneliness", 0f);

        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_socialise": HandleDoSocialise(agent);      break;
                case "arrived":      HandleArrived(agent, data);    break;
            }

            // Another task taking priority — cancel any pending travel to the park.
            if (evt.StartsWith("do_") && evt != "do_socialise")
                CancelTravelIntent(agent);
        };

        agent.OnEvent += eventHandler;
    }

    public void Tick(AgentV2 agent)
    {
        if (!isVisiting) return;

        visitTimer -= Time.deltaTime;
        // TODO: roll HealthManager.parkInjuryChancePerSecond each frame

        if (visitTimer <= 0f)
            FinishVisit(agent);
    }

    public void SlowTick(AgentV2 agent)
    {
        float rate = LonelinessRatePerSlowTick;
        if      (agent.Tags.Contains("life_baby"))    rate = 0f;
        else if (agent.Tags.Contains("life_toddler")) rate = 0.05f;

        agent.ModifyStat("loneliness", rate, 0f, 100f);
    }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoSocialise(AgentV2 agent)
    {
        if (agent.BuildingManager == null) return;

        var tile = agent.BuildingManager.FindNearest<Park>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position));

        if (tile == null) return;

        agent.CurrentTask = "socialise";
        agent.RaiseEvent("move_to", TileToWorld(agent, tile.Value));
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "socialise") return;

        isVisiting = true;
        visitTimer = VisitDuration;
        // CurrentTask stays "socialise" until FinishVisit clears it.
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask == "socialise" && !isVisiting)
            agent.CurrentTask = "";
    }

    // ── Internal helpers ───────────────────────────────────────────────────────
    private void FinishVisit(AgentV2 agent)
    {
        isVisiting = false;
        agent.ModifyStat("loneliness", -LonelinessRestored, 0f, 100f);
        agent.CurrentTask = "";

        if (agent.GetModule<HomeModule>()?.HasHome == true)
            agent.RaiseEvent("do_go_home");
    }

    private static Vector3 TileToWorld(AgentV2 agent, Vector3Int tile)
        => agent.BuildingsTilemap != null
            ? agent.BuildingsTilemap.CellToWorld(tile)
            : new Vector3(tile.x, tile.y, 0f);
}
