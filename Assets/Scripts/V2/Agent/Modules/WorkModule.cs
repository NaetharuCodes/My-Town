using System;
using UnityEngine;

// Manages employment state and handles all work-related behaviour.
//
// Blackboard tags written:
//   employed     → agent has a job
//   work_hours   → the assigned shift is currently active
//
// Events consumed:
//   do_seek_work    → find and accept a job at the nearest commercial building
//   do_go_to_work   → navigate to the employer; check in on arrival
//   arrived         → check in at workplace when CurrentTask == "work"
//
// Events raised:
//   move_to         → via LocomotionModule
//
// Public API (called by AgentManager / building demolition):
//   Hire(CommercialBuilding, Shift)
//   LoseJob()
public class WorkModule : IAgentModule
{
    // ── Employment state ───────────────────────────────────────────────────────
    public bool               HasJob             { get; private set; } = false;
    public CommercialBuilding Employer           { get; private set; }
    public Shift              AssignedShift      { get; private set; }
    public bool               IsCurrentlyWorking => isAtWork;

    private bool isAtWork       = false;
    private int  lastCheckedHour = -1;

    private Action<string, object> eventHandler;
    private Action<int>            onHourChanged;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_seek_work":  HandleDoSeekWork(agent);    break;
                case "do_go_to_work": HandleDoGoToWork(agent);    break;
                case "arrived":       HandleArrived(agent, data); break;
                case "path_failed":
                    if (agent.CurrentTask == "work_travel")
                        agent.CurrentTask = "";
                    break;
            }

            // Another task displaced work travel — cancel movement intent.
            if (evt.StartsWith("do_") && evt != "do_go_to_work" && evt != "do_seek_work")
                CancelTravelIntent(agent);
        };
        agent.OnEvent += eventHandler;

        // Track the current hour so we can update the work_hours tag.
        if (agent.TimeManager != null)
        {
            onHourChanged = hour =>
            {
                lastCheckedHour = hour;
                UpdateWorkHoursTag(agent);
            };
            agent.TimeManager.OnHourChanged += onHourChanged;
            lastCheckedHour = agent.TimeManager.CurrentHour;
        }
    }

    public void Tick(AgentV2 agent)
    {
        if (!isAtWork) return;

        // Check each frame whether the shift has ended.
        if (AssignedShift != null && !AssignedShift.IsActiveAt(lastCheckedHour))
        {
            Employer.WorkerCheckOut(agent);
            Employer.ExitV2(agent);
            isAtWork = false;
            agent.CurrentTask = "";
            agent.Tags.Remove("at_work");

            // TODO: roll HealthManager.workInjuryChancePerSecond while working
        }
    }

    public void SlowTick(AgentV2 agent)
    {
        UpdateWorkHoursTag(agent);

        // Retry seeking work each slow tick while unemployed and idle.
        // Handles the case where all vacancies were filled at last attempt but one has since opened.
        if (!HasJob && agent.Tags.Contains("can_work") && agent.CurrentTask == "")
            HandleDoSeekWork(agent);
    }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
        if (agent.TimeManager != null && onHourChanged != null)
            agent.TimeManager.OnHourChanged -= onHourChanged;

        if (isAtWork && Employer != null)
            Employer.ExitV2(agent);
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    public void Hire(AgentV2 agent, CommercialBuilding employer, Shift shift)
    {
        Employer      = employer;
        AssignedShift = shift;
        HasJob        = true;
        agent.Tags.Add("employed");
        UpdateWorkHoursTag(agent);
    }

    public void LoseJob(AgentV2 agent)
    {
        if (isAtWork && Employer != null)
            Employer.ExitV2(agent);

        AssignedShift?.Unassign(agent);
        Employer      = null;
        AssignedShift = null;
        HasJob        = false;
        isAtWork      = false;
        agent.Tags.Remove("employed");
        agent.Tags.Remove("work_hours");
        agent.Tags.Remove("at_work");

        if (agent.CurrentTask is "work" or "work_travel")
            agent.CurrentTask = "";
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoSeekWork(AgentV2 agent)
    {
        if (HasJob || agent.BuildingManager == null) return;

        var tile = agent.BuildingManager.FindNearest<CommercialBuilding>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position),
            b => b.HasShiftVacancy());

        if (tile == null) return;

        var building = agent.BuildingManager.GetBuildingAt(tile.Value) as CommercialBuilding;
        if (building == null) return;

        Shift shift = building.TryHire(agent);
        if (shift != null)
        {
            Hire(agent, building, shift);
            Debug.Log($"{agent.Name}: hired at {building.name}");
        }
    }

    private void HandleDoGoToWork(AgentV2 agent)
    {
        if (!HasJob || Employer == null) return;
        if (isAtWork) return;  // Already there.

        agent.CurrentTask = "work_travel";
        Vector3 dest = agent.BuildingsTilemap != null
            ? agent.BuildingsTilemap.GetCellCenterWorld(Employer.gridPosition)
            : new Vector3(Employer.gridPosition.x, Employer.gridPosition.y, 0f);
        agent.RaiseEvent("move_to", dest);
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "work_travel") return;

        Employer.WorkerCheckIn(agent);
        Employer.EnterV2(agent);
        // TODO: Police/FireStation special case → "patrolling" tag instead of "at_work"
        isAtWork = true;
        agent.CurrentTask = "work";
        agent.Tags.Add("at_work");
        Debug.Log($"{agent.Name}: checked in at {Employer.name}");
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask == "work_travel")
        {
            agent.CurrentTask = "";
            // isAtWork stays false — agent will re-path when DecisionModule next fires do_go_to_work.
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private void UpdateWorkHoursTag(AgentV2 agent)
    {
        bool shiftActive = HasJob && AssignedShift != null
                           && AssignedShift.IsActiveAt(lastCheckedHour);

        if (shiftActive) agent.Tags.Add("work_hours");
        else             agent.Tags.Remove("work_hours");
    }
}
