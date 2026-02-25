using UnityEngine;

public class TestConsumer : IAgentModule
{
    public void Initialize(AgentV2 agent)
    {
        Debug.Log($"{agent.Name}: Test consumer initialized");

        agent.OnEvent += (eventType, data) =>
        {
            if (eventType == "test_tick")
            {
                Debug.Log($"{agent.Name}: Consumer heard test_tick!");
            }
        };
    }

    public void Tick(AgentV2 agent) { }

    public void Cleanup(AgentV2 agent) { }
}