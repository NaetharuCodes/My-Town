using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A single hospital bed. Tracks which agent is occupying it.
/// </summary>
[System.Serializable]
public class HospitalBed
{
    public Agent occupant;
    public bool IsOccupied => occupant != null;
}

/// <summary>
/// A Hospital treats Serious and Critical conditions.
/// Capacity is determined by the number of beds configured in the Inspector.
/// Agents wait in Idle if all beds are full and retry periodically.
/// </summary>
public class Hospital : MedicalBuilding
{
    [Header("Hospital")]
    [Tooltip("Configure the number of beds in the Inspector. Each bed holds one patient.")]
    public List<HospitalBed> beds = new List<HospitalBed>();

    [Tooltip("Cost charged to the patient for a hospital stay.")]
    public int treatmentCost = 200;

    public override bool CanTreat(ConditionSeverity severity)
        => severity == ConditionSeverity.Serious || severity == ConditionSeverity.Critical;

    public override bool TryAdmit(Agent agent)
    {
        HospitalBed free = beds.Find(b => !b.IsOccupied);
        if (free == null) return false;
        free.occupant = agent;
        currentPatients.Add(agent);
        return true;
    }

    public override void DischargePatient(Agent agent)
    {
        HospitalBed bed = beds.Find(b => b.occupant == agent);
        if (bed != null) bed.occupant = null;
        currentPatients.Remove(agent);
    }

    public override void TreatPatient(Agent agent)
    {
        agent.TreatConditions(ConditionSeverity.Serious);
        agent.TreatConditions(ConditionSeverity.Critical);
        if (agent.TrySpend(treatmentCost))
            treasury += treatmentCost;
        EventLog.Log($"{agent.agentName} was discharged from {buildingName}.");
        Debug.Log($"{agent.agentName} discharged from {buildingName}. Cost: ${treatmentCost}");
    }

    protected override void SetupDefaultShifts()
    {
        // Day shift: 7am–19pm
        shifts.Add(new Shift
        {
            startHour       = 7,
            durationHours   = 12,
            workersRequired = 2,
            wage            = 500,
            payFrequency    = PayFrequency.Weekly
        });
        // Night shift: 19pm–7am
        shifts.Add(new Shift
        {
            startHour       = 19,
            durationHours   = 12,
            workersRequired = 2,
            wage            = 550,
            payFrequency    = PayFrequency.Weekly
        });
    }

    public override bool Interact(Agent agent) => false; // patients are admitted via TryAdmit
}
