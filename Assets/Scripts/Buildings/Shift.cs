using System.Collections.Generic;

// Represents a single work shift within a building.
// [System.Serializable] lets the shift list appear in the Unity Inspector
// so designers can configure hours, wages and staffing per building.
[System.Serializable]
public class Shift
{
    public int startHour = 8;
    public int durationHours = 8;
    public int workersRequired = 1;
    public int wage = 280;
    public PayFrequency payFrequency = PayFrequency.Weekly;

    // Runtime state — not serialized (private fields are ignored by Unity serializer)
    private List<Agent>    assignedWorkers   = new List<Agent>();
    private HashSet<Agent> presentWorkers    = new HashSet<Agent>();
    private List<AgentV2>    assignedWorkersV2 = new List<AgentV2>();
    private HashSet<AgentV2> presentWorkersV2  = new HashSet<AgentV2>();

    // Vacancy accounts for both V1 and V2 workers combined.
    public bool HasVacancy => assignedWorkers.Count + assignedWorkersV2.Count < workersRequired;

    public IReadOnlyList<Agent>   AssignedWorkers   => assignedWorkers;
    public IReadOnlyList<AgentV2> AssignedWorkersV2 => assignedWorkersV2;
    public bool HasPresentWorker => presentWorkers.Count > 0 || presentWorkersV2.Count > 0;
    public IReadOnlyCollection<Agent> PresentWorkers => presentWorkers;

    public bool IsActiveAt(int hour)
    {
        int endHour = startHour + durationHours;
        if (endHour <= 24)
            return hour >= startHour && hour < endHour;
        // Shift crosses midnight (e.g. 22:00 – 06:00)
        return hour >= startHour || hour < endHour % 24;
    }

    // ── V1 Agent overloads ─────────────────────────────────────────────────────
    public bool TryAssign(Agent agent)
    {
        if (!HasVacancy || assignedWorkers.Contains(agent)) return false;
        assignedWorkers.Add(agent);
        return true;
    }

    public void Unassign(Agent agent)
    {
        assignedWorkers.Remove(agent);
        presentWorkers.Remove(agent);
    }

    public void CheckIn(Agent agent)  => presentWorkers.Add(agent);
    public void CheckOut(Agent agent) => presentWorkers.Remove(agent);

    // ── V2 AgentV2 overloads ───────────────────────────────────────────────────
    public bool TryAssign(AgentV2 agent)
    {
        if (!HasVacancy || assignedWorkersV2.Contains(agent)) return false;
        assignedWorkersV2.Add(agent);
        return true;
    }

    public void Unassign(AgentV2 agent)
    {
        assignedWorkersV2.Remove(agent);
        presentWorkersV2.Remove(agent);
    }

    public bool IsAssigned(AgentV2 agent) => assignedWorkersV2.Contains(agent);

    public void CheckIn(AgentV2 agent)  => presentWorkersV2.Add(agent);
    public void CheckOut(AgentV2 agent) => presentWorkersV2.Remove(agent);
}

public enum PayFrequency { Daily, Weekly, Monthly }
