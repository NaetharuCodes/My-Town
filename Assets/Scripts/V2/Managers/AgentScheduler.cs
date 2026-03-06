using System.Collections.Generic;
using UnityEngine;

// Drives SlowTick for all AgentV2 instances, staggered across frames.
// Each agent is guaranteed exactly one SlowTick per slowTickInterval seconds.
// Uses an accumulator so small populations aren't over-ticked each frame.
public class AgentScheduler : MonoBehaviour
{
    public static AgentScheduler Instance;

    [Tooltip("How often (in seconds) each agent receives a SlowTick.")]
    [SerializeField] private float slowTickInterval = 1f;

    private readonly List<AgentV2> agents = new();
    private int   currentIndex  = 0;
    private float accumulator   = 0f;

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

        // Accumulate fractional work. Each second we want to tick each agent once,
        // so we advance by agents.Count / slowTickInterval per second.
        accumulator += agents.Count * Time.deltaTime / slowTickInterval;

        // Process whole ticks only — no Mathf.Max(1) which would over-tick small populations.
        while (accumulator >= 1f)
        {
            agents[currentIndex].SlowTick();
            currentIndex = (currentIndex + 1) % agents.Count;
            accumulator -= 1f;
        }
    }
}
