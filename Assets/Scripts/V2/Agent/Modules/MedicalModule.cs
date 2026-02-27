using System;
using System.Collections.Generic;
using UnityEngine;

// Manages health conditions and handles medical-seeking behaviour.
//
// Blackboard tags written:
//   needs_hospital  → has a symptomatic Serious or Critical condition
//   needs_doctor    → has a symptomatic Mild condition
//   is_sick         → has any symptomatic condition
//
// Events consumed:
//   do_seek_doctor    → navigate to nearest open DoctorsSurgery
//   do_seek_hospital  → navigate to nearest Hospital
//   arrived           → attempt admission when CurrentTask is a medical task
//
// Events raised:
//   move_to  → via LocomotionModule
//
// Public API:
//   ContractCondition(HealthCondition)
//   TreatConditions(ConditionSeverity)    ← called by medical buildings
//   IsSick, NeedsDoctor, NeedsHospital   ← mirrors V1 properties
public class MedicalModule : IAgentModule
{
    // ── Condition state ────────────────────────────────────────────────────────
    public List<ActiveCondition> Conditions { get; } = new();

    public bool IsSick       => Conditions.Exists(c => c.symptomatic);
    public bool NeedsDoctor  => Conditions.Exists(c => c.symptomatic && c.data.severity == ConditionSeverity.Mild);
    public bool NeedsHospital => Conditions.Exists(c => c.symptomatic && c.data.severity >= ConditionSeverity.Serious);

    // ── Treatment state ────────────────────────────────────────────────────────
    private MedicalBuilding currentBuilding;
    private MedicalBuilding travelTarget;
    private float           treatmentTimer    = 0f;
    private bool            isBeingTreated    = false;

    // ── Disease spread timer ───────────────────────────────────────────────────
    private float spreadTimer = 0f;
    private const float SpreadInterval = 5f;

    private Action<string, object> eventHandler;
    private Action<int>            onNewDay;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_seek_doctor":   HandleDoSeekDoctor(agent);   break;
                case "do_seek_hospital": HandleDoSeekHospital(agent); break;
                case "arrived":          HandleArrived(agent, data);  break;
            }

            if (evt.StartsWith("do_") && evt != "do_seek_doctor" && evt != "do_seek_hospital")
                CancelTravelIntent(agent);
        };
        agent.OnEvent += eventHandler;

        if (agent.TimeManager != null)
        {
            onNewDay = _ => ProgressConditions(agent);
            agent.TimeManager.OnNewDay += onNewDay;
        }
    }

    public void Tick(AgentV2 agent)
    {
        // Treatment countdown.
        if (isBeingTreated)
        {
            treatmentTimer -= Time.deltaTime;
            if (treatmentTimer <= 0f)
                FinishTreatment(agent);
        }

        // Disease spread check every SpreadInterval seconds.
        spreadTimer += Time.deltaTime;
        if (spreadTimer >= SpreadInterval)
        {
            spreadTimer = 0f;
            CheckDiseaseSpread(agent);
        }
    }

    public void SlowTick(AgentV2 agent) => UpdateHealthTags(agent);

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
        if (agent.TimeManager != null && onNewDay != null)
            agent.TimeManager.OnNewDay -= onNewDay;
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    public void ContractCondition(HealthCondition condition)
    {
        // Prevent re-infection with the same condition.
        if (Conditions.Exists(c => c.data == condition)) return;
        Conditions.Add(new ActiveCondition { data = condition });
    }

    public void TreatConditions(ConditionSeverity severity)
    {
        Conditions.RemoveAll(c => c.data.severity == severity);
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoSeekDoctor(AgentV2 agent)
    {
        if (agent.BuildingManager == null) return;

        var tile = agent.BuildingManager.FindNearest<DoctorsSurgery>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position),
            b => b.IsOpen());

        if (tile == null) return;

        travelTarget = agent.BuildingManager.GetBuildingAt(tile.Value) as MedicalBuilding;
        if (travelTarget == null) return;

        agent.CurrentTask = "doctor_travel";
        agent.RaiseEvent("move_to", TileToWorld(agent, tile.Value));
    }

    private void HandleDoSeekHospital(AgentV2 agent)
    {
        if (agent.BuildingManager == null) return;

        // Hospitals don't have open/close hours — always available.
        var tile = agent.BuildingManager.FindNearest<Hospital>(
            agent.BuildingsTilemap.WorldToCell(agent.transform.position));

        if (tile == null) return;

        travelTarget = agent.BuildingManager.GetBuildingAt(tile.Value) as MedicalBuilding;
        if (travelTarget == null) return;

        agent.CurrentTask = "hospital_travel";
        agent.RaiseEvent("move_to", TileToWorld(agent, tile.Value));
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "doctor_travel" && agent.CurrentTask != "hospital_travel") return;
        if (travelTarget == null) return;

        bool admitted = travelTarget.TryAdmit(agent);
        if (!admitted)
        {
            // Building is full — clear intent; DecisionModule will retry next tick.
            travelTarget      = null;
            agent.CurrentTask = "";
            return;
        }

        bool isHospital = agent.CurrentTask == "hospital_travel";
        currentBuilding = travelTarget;
        travelTarget    = null;
        treatmentTimer  = currentBuilding.treatmentDuration;
        isBeingTreated  = true;

        // Mark matching conditions as receiving treatment.
        var targetSeverity = isHospital ? ConditionSeverity.Serious : ConditionSeverity.Mild;
        foreach (var c in Conditions)
        {
            if (c.data.severity >= targetSeverity)
                c.receivingTreatment = true;
        }

        agent.CurrentTask = isHospital ? "hospital" : "doctor";
        Debug.Log($"{agent.Name}: admitted to {currentBuilding.name}");
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask is "doctor_travel" or "hospital_travel")
        {
            agent.CurrentTask = "";
            travelTarget = null;
        }
    }

    // ── Internal helpers ───────────────────────────────────────────────────────
    private void FinishTreatment(AgentV2 agent)
    {
        isBeingTreated = false;

        currentBuilding.TreatPatient(agent);
        currentBuilding.DischargePatient(agent);
        currentBuilding = null;
        agent.CurrentTask = "";

        UpdateHealthTags(agent);

        if (agent.GetModule<HomeModule>()?.HasHome == true)
            agent.RaiseEvent("do_go_home");
    }

    private void UpdateHealthTags(AgentV2 agent)
    {
        if (NeedsHospital) agent.Tags.Add("needs_hospital"); else agent.Tags.Remove("needs_hospital");
        if (NeedsDoctor)   agent.Tags.Add("needs_doctor");   else agent.Tags.Remove("needs_doctor");
        if (IsSick)        agent.Tags.Add("is_sick");        else agent.Tags.Remove("is_sick");
    }

    private void ProgressConditions(AgentV2 agent)
    {
        for (int i = Conditions.Count - 1; i >= 0; i--)
        {
            var c = Conditions[i];
            c.daysWithCondition++;

            // Incubation → symptomatic.
            if (!c.symptomatic && c.daysWithCondition >= c.data.incubationDays)
                c.symptomatic = true;

            // Untreated progression.
            if (c.symptomatic && !c.receivingTreatment && c.data.progressesIfUntreated
                && c.daysWithCondition >= c.data.daysUntilProgression)
            {
                if (c.data.progressionCondition != null)
                {
                    Conditions.RemoveAt(i);
                    ContractCondition(c.data.progressionCondition);
                }
                else
                {
                    // Critical with no further progression — die.
                    Debug.Log($"{agent.Name}: died from untreated {c.data.conditionName}");
                    // TODO: AgentManager cleanup — wire up V1/V2 manager API
                    UnityEngine.Object.Destroy(agent.gameObject);
                    return;
                }
            }

            // Natural recovery roll.
            if (c.symptomatic && UnityEngine.Random.value < c.data.naturalRecoveryChancePerDay)
            {
                Conditions.RemoveAt(i);
            }
        }

        UpdateHealthTags(agent);
    }

    private void CheckDiseaseSpread(AgentV2 agent)
    {
        // TODO: iterate nearby AgentV2 instances and spread contagious conditions.
        // Requires an AgentManager or spatial query in V2.
        // Pattern from V1: for each symptomatic contagious condition,
        //   for each other agent within spreadRadius,
        //     roll spreadChancePerExposure → ContractCondition on target.
    }

    private static Vector3 TileToWorld(AgentV2 agent, Vector3Int tile)
        => agent.BuildingsTilemap != null
            ? agent.BuildingsTilemap.CellToWorld(tile)
            : new Vector3(tile.x, tile.y, 0f);
}
