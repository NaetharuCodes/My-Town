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
    private List<Agent> assignedWorkers = new List<Agent>();
    private HashSet<Agent> presentWorkers = new HashSet<Agent>();

    public bool HasVacancy => assignedWorkers.Count < workersRequired;
    public IReadOnlyList<Agent> AssignedWorkers => assignedWorkers;
    public bool HasPresentWorker => presentWorkers.Count > 0;

    public bool IsActiveAt(int hour)
    {
        int endHour = startHour + durationHours;
        if (endHour <= 24)
            return hour >= startHour && hour < endHour;
        // Shift crosses midnight (e.g. 22:00 – 06:00)
        return hour >= startHour || hour < endHour % 24;
    }

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

    public void CheckIn(Agent agent) => presentWorkers.Add(agent);
    public void CheckOut(Agent agent) => presentWorkers.Remove(agent);
}

public enum PayFrequency { Daily, Weekly, Monthly }
