using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A Doctor's Surgery treats Mild conditions.
/// Walk-in service limited by maxPatients capacity.
/// Agents with Serious or Critical conditions should go to the Hospital instead.
/// </summary>
public class DoctorsSurgery : MedicalBuilding
{
    [Header("Surgery")]
    [Tooltip("Maximum number of patients that can be seen simultaneously.")]
    public int maxPatients = 4;

    public override bool CanTreat(ConditionSeverity severity)
        => severity == ConditionSeverity.Mild;

    public override bool TryAdmit(Agent agent)
    {
        if (!IsOpen() || currentPatients.Count >= maxPatients) return false;
        currentPatients.Add(agent);
        return true;
    }

    public override void DischargePatient(Agent agent)
    {
        currentPatients.Remove(agent);
    }

    public override void TreatPatient(Agent agent)
    {
        agent.TreatConditions(ConditionSeverity.Mild);
        if (agent.TrySpend(consultationFee))
            treasury += consultationFee;
        EventLog.Log($"{agent.agentName} was treated at {buildingName}.");
        Debug.Log($"{agent.agentName} treated at {buildingName}. Fee: ${consultationFee}");
    }

    protected override void SetupDefaultShifts()
    {
        int start = Random.Range(7, 10);
        shifts.Add(new Shift
        {
            startHour      = start,
            durationHours  = 10,
            workersRequired = 1,
            wage           = 400,
            payFrequency   = PayFrequency.Weekly
        });
    }

    public override bool Interact(Agent agent) => false; // patients are admitted via TryAdmit
}
