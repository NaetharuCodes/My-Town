
public interface IAgentModule
{
    void Initialize(Agent agent);

    void Tick(Agent agent);

    void Cleanup(Agent agent);
}