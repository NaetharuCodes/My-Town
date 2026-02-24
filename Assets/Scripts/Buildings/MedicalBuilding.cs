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

    protected List<Agent> currentPatients = new List<Agent>();

    /// <summary>Returns true if this building can treat the given severity.</summary>
    public abstract bool CanTreat(ConditionSeverity severity);

    /// <summary>
    /// Attempts to admit the agent as a patient.
    /// Returns true if admitted; false if the building is full or closed.
    /// </summary>
    public abstract bool TryAdmit(Agent agent);

    /// <summary>Releases the agent from this building's patient list.</summary>
    public abstract void DischargePatient(Agent agent);

    /// <summary>Called when treatment is complete — applies cures and charges the fee.</summary>
    public abstract void TreatPatient(Agent agent);

    // When the building is demolished, discharge all current patients.
    protected override void OnDestroy()
    {
        base.OnDestroy();
        foreach (Agent patient in new List<Agent>(currentPatients))
            DischargePatient(patient);
    }
}
