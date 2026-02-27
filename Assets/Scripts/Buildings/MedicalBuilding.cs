using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract base for DoctorsSurgery and Hospital.
/// Extends CommercialBuilding so medical buildings have staff, shifts, and payroll.
/// </summary>
public abstract class MedicalBuilding : CommercialBuilding
{
    [Header("Medical")]
    [Tooltip("Cost charged to the patient per visit/stay.")]
    public int consultationFee = 50;
    [Tooltip("Real seconds the agent spends receiving treatment.")]
    public float treatmentDuration = 15f;

    protected List<Agent>   currentPatients   = new List<Agent>();
    protected List<AgentV2> currentPatientsV2 = new List<AgentV2>();

    /// <summary>Returns true if this building can treat the given severity.</summary>
    public abstract bool CanTreat(ConditionSeverity severity);

    // ── V1 Agent overloads ─────────────────────────────────────────────────────
    public abstract bool TryAdmit(Agent agent);
    public abstract void DischargePatient(Agent agent);
    public abstract void TreatPatient(Agent agent);

    // ── V2 AgentV2 overloads ───────────────────────────────────────────────────
    public abstract bool TryAdmit(AgentV2 agent);
    public abstract void DischargePatient(AgentV2 agent);
    public abstract void TreatPatient(AgentV2 agent);

    // When the building is demolished, discharge all current patients.
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (Agent patient in new List<Agent>(currentPatients))
            DischargePatient(patient);
        foreach (AgentV2 patient in new List<AgentV2>(currentPatientsV2))
            DischargePatient(patient);
    }
}
