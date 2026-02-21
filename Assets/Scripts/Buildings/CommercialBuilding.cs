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

    // Assigns the agent to the first shift with a vacancy.
    // Returns the shift they were assigned to, or null if no vacancy.
    public Shift TryHire(Agent agent)
    {
        foreach (var shift in shifts)
            if (shift.TryAssign(agent))
                return shift;
        return null;
    }

    // Returns the shift this agent is assigned to, or null.
    public Shift GetShiftFor(Agent agent)
    {
        foreach (var shift in shifts)
            if (shift.AssignedWorkers.Contains(agent))
                return shift;
        return null;
    }

    // --- Worker presence ---

    public void WorkerCheckIn(Agent agent)
    {
        foreach (var shift in shifts)
            if (shift.AssignedWorkers.Contains(agent))
                shift.CheckIn(agent);
    }

    public void WorkerCheckOut(Agent agent)
    {
        foreach (var shift in shifts)
            shift.CheckOut(agent);
    }

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
        foreach (var worker in shift.AssignedWorkers)
        {
            if (treasury >= shift.wage)
            {
                treasury -= shift.wage;
                worker.ReceiveWage(shift.wage);
            }
            else
            {
                Debug.Log($"{buildingName} can't afford to pay {worker.agentName}!");
            }
        }
    }

    // Returns the names of workers assigned to the first shift (for saving).
    public List<string> GetAssignedWorkerNames()
    {
        var names = new List<string>();
        if (shifts.Count > 0)
            foreach (var worker in shifts[0].AssignedWorkers)
                names.Add(worker.agentName);
        return names;
    }

    public override bool Interact(Agent agent)
    {
        return false;
    }
}
