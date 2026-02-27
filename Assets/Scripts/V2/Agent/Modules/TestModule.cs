using UnityEngine;

public class TestModule : IAgentModule
{
    public void Initialize(AgentV2 agent)
    {
        agent.SetStat("test_value", 0f);
        Debug.Log($"{agent.Name}: TestModule initialized");
    }

    public void Tick(AgentV2 agent)
    {
        agent.ModifyStat("test_value", Time.deltaTime);

        if (agent.GetStat("test_value") > 5f)
        {
            Debug.Log($"{agent.Name}: 5 seconds have passed!");
            agent.SetStat("test_value", 0f);
            agent.RaiseEvent("test_tick");
        }
    }

    public void SlowTick(AgentV2 agent) { }

    public void Cleanup(AgentV2 agent)
    {
        Debug.Log($"{agent.Name}: TestModule removed");
    }
}