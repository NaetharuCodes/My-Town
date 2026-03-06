using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Base class for all commercial/service buildings.
// Handles the shift roster, worker check-in/out, open/closed state,
// and payroll. Subclasses define what service they provide via Interact()
// and configure their default shifts via SetupDefaultShifts().
public class CommercialBuilding : Building
{
    [Header("Shifts")]
    public List<Shift> shifts = new List<Shift>();

    protected TimeManager timeManager;

    protected virtual void Awake()
    {
        // Populate default shifts before anything else runs.
        // Subclasses override SetupDefaultShifts() to define their roster.
        if (shifts.Count == 0)
            SetupDefaultShifts();
    }

    protected virtual void Start()
    {
        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
        {
            timeManager.OnNewDay += OnNewDay;
            timeManager.OnNewWeek += OnNewWeek;
        }
    }

    protected virtual void OnDestroy()
    {
        if (timeManager != null)
        {
            timeManager.OnNewDay -= OnNewDay;
            timeManager.OnNewWeek -= OnNewWeek;
        }
    }

    // Subclasses override this to define their default shift configuration.
    protected virtual void SetupDefaultShifts() { }

    // --- Open/closed state ---

    // A building is open if at least one shift is currently active
    // AND has a worker physically checked in.
    public bool IsOpen()
    {
        if (timeManager == null) return false;
        int hour = timeManager.CurrentHour;
        foreach (var shift in shifts)
            if (shift.IsActiveAt(hour) && shift.HasPresentWorker)
                return true;
        return false;
    }

    // Returns the shift that is currently scheduled (regardless of staffing).
    public Shift GetActiveShift()
    {
        if (timeManager == null) return null;
        int hour = timeManager.CurrentHour;
        foreach (var shift in shifts)
            if (shift.IsActiveAt(hour))
                return shift;
        return null;
    }

    // True if any shift still has an unfilled worker slot.
    public bool HasShiftVacancy()
    {
        foreach (var shift in shifts)
            if (shift.HasVacancy) return true;
        return false;
    }

    // --- Hiring ---

    public Shift TryHire(AgentV2 agent)
    {
        foreach (var shift in shifts)
            if (shift.TryAssign(agent))
                return shift;
        return null;
    }

    public Shift GetShiftFor(AgentV2 agent)
    {
        foreach (var shift in shifts)
            if (shift.IsAssigned(agent))
                return shift;
        return null;
    }

    public void WorkerCheckIn(AgentV2 agent)
    {
        foreach (var shift in shifts)
            if (shift.IsAssigned(agent))
                shift.CheckIn(agent);
    }

    public void WorkerCheckOut(AgentV2 agent)
    {
        foreach (var shift in shifts)
            shift.CheckOut(agent);
    }

    public virtual bool Interact(AgentV2 agent) => false;

    // --- Payroll ---

    void OnNewDay(int day)
    {
        foreach (var shift in shifts)
            if (shift.payFrequency == PayFrequency.Daily)
                PayShift(shift);
    }

    void OnNewWeek()
    {
        foreach (var shift in shifts)
            if (shift.payFrequency == PayFrequency.Weekly)
                PayShift(shift);
    }

    void PayShift(Shift shift)
    {
        foreach (var worker in shift.AssignedWorkersV2)
        {
            if (treasury >= shift.wage)
            {
                treasury -= shift.wage;
                worker.ModifyStat("bank_balance", shift.wage);
            }
            else
            {
                Debug.Log($"{buildingName} can't afford to pay {worker.Name}!");
            }
        }
    }

}
