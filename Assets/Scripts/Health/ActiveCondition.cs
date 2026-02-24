/// <summary>
/// Runtime instance of a HealthCondition on a specific agent.
/// Tracks how long the agent has had the condition and whether it is actively being treated.
/// </summary>
public class ActiveCondition
{
    public HealthCondition data;

    /// <summary>Days the agent has had this condition (incremented each in-game day).</summary>
    public float daysWithCondition;

    /// <summary>True once the incubation period has passed and symptoms are visible.</summary>
    public bool symptomatic;

    /// <summary>True while the agent is at a medical building receiving treatment.</summary>
    public bool receivingTreatment;
}
