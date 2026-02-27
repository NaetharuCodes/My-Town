using System.Collections.Generic;
using UnityEngine;

// Drives SlowTick for all AgentV2 instances, staggered across frames.
// All agents are guaranteed one SlowTick per slowTickInterval seconds.
// At 60fps with 200 agents and a 1s interval: ~4 agents/frame — negligible cost.
public class AgentScheduler : MonoBehaviour
{
    public static AgentScheduler Instance;

    [Tooltip("How often (in seconds) each agent receives a SlowTick.")]
    [SerializeField] private float slowTickInterval = 1f;

    private readonly List<AgentV2> agents = new();
    private int currentIndex = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void Register(AgentV2 agent)
    {
        agents.Add(agent);
    }

    public void Unregister(AgentV2 agent)
    {
        // If the removed agent is behind the current index, step back to avoid skipping the next agent.
        int removedAt = agents.IndexOf(agent);
        if (removedAt >= 0 && removedAt < currentIndex)
            currentIndex--;

        agents.Remove(agent);

        if (currentIndex >= agents.Count)
            currentIndex = 0;
    }

    private void Update()
    {
        if (agents.Count == 0) return;

        // Spread all agents evenly across the interval.
        int perFrame = Mathf.Max(1, Mathf.CeilToInt(agents.Count * Time.deltaTime / slowTickInterval));

        for (int i = 0; i < perFrame; i++)
        {
            agents[currentIndex].SlowTick();
            currentIndex = (currentIndex + 1) % agents.Count;
        }
    }
}
