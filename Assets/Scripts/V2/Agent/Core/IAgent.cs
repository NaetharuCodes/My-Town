
public interface IAgentModule
{
    void Initialize(AgentV2 agent);

    void Tick(AgentV2 agent);

    void Cleanup(AgentV2 agent);
}