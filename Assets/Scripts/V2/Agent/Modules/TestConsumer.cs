using System;
using UnityEngine;

public class TestConsumer : IAgentModule
{
    private Action<string, object> eventHandler;

    public void Initialize(AgentV2 agent)
    {
        Debug.Log($"{agent.Name}: Test consumer initialized");

        eventHandler = (eventType, data) =>
        {
            if (eventType == "test_tick")
                Debug.Log($"{agent.Name}: Consumer heard test_tick!");
        };

        agent.OnEvent += eventHandler;
    }

    public void Tick(AgentV2 agent) { }

    public void SlowTick(AgentV2 agent) { }

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
    }
}
