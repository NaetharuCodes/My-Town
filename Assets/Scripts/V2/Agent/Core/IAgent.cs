
public interface IAgentModule
{
    void Initialize(AgentV2 agent);

    // Called every frame — use only for things that need smooth per-frame updates (e.g. movement).
    void Tick(AgentV2 agent);

    // Called ~once per second, staggered across agents by AgentScheduler.
    // Use for needs, decisions, and anything that doesn't need per-frame resolution.
    void SlowTick(AgentV2 agent);

    void Cleanup(AgentV2 agent);
}