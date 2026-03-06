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

    public override bool TryAdmit(AgentV2 agent)
    {
        if (!IsOpen() || TotalPatients >= maxPatients) return false;
        currentPatientsV2.Add(agent);
        return true;
    }

    public override void DischargePatient(AgentV2 agent)
    {
        currentPatientsV2.Remove(agent);
    }

    public override void TreatPatient(AgentV2 agent)
    {
        agent.GetModule<MedicalModule>()?.TreatConditions(ConditionSeverity.Mild);
        if (agent.GetStat("bank_balance") >= consultationFee)
        {
            agent.ModifyStat("bank_balance", -consultationFee);
            treasury += consultationFee;
        }
        Debug.Log($"{agent.Name} treated at {buildingName}. Fee: ${consultationFee}");
    }

    public override bool Interact(AgentV2 agent) => false; // patients admitted via TryAdmit

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

    private int TotalPatients => currentPatientsV2.Count;
}
