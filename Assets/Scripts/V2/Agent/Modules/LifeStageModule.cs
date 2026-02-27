using UnityEngine;

// Sets capability tags on the blackboard based on this agent's life stage.
// Add this module FIRST during agent setup so all other modules see the correct tags.
//
// Tags set (life stage identity — exactly one):
//   life_baby, life_toddler, life_young_child, life_older_child,
//   life_teen, life_adult, life_elder, life_venerable_elder
//
// Tags set (capabilities — mirrors V1 CanXxx() methods):
//   can_cook         YoungChild+
//   can_shop         Teen+
//   can_work         Adult+
//   can_visit_park   YoungChild+
//   eligible_for_preschool   Toddler only
//   eligible_for_school      YoungChild–Teen
//   needs_parent_feed        Baby or Toddler
public class LifeStageModule : IAgentModule
{
    private readonly LifeStage lifeStage;
    private readonly int       ageInYears;

    public LifeStageModule(LifeStage lifeStage, int ageInYears = 25)
    {
        this.lifeStage  = lifeStage;
        this.ageInYears = ageInYears;
    }

    public void Initialize(AgentV2 agent)
    {
        // ── Life-stage identity tag ────────────────────────────────────────────
        string stageTag = lifeStage switch
        {
            LifeStage.Baby           => "life_baby",
            LifeStage.Toddler        => "life_toddler",
            LifeStage.YoungChild     => "life_young_child",
            LifeStage.OlderChild     => "life_older_child",
            LifeStage.Teen           => "life_teen",
            LifeStage.Adult          => "life_adult",
            LifeStage.Elder          => "life_elder",
            LifeStage.VenerableElder => "life_venerable_elder",
            _                        => "life_adult"
        };
        agent.Tags.Add(stageTag);

        // ── Capability tags ────────────────────────────────────────────────────
        if (lifeStage >= LifeStage.YoungChild) agent.Tags.Add("can_cook");
        if (lifeStage >= LifeStage.Teen)       agent.Tags.Add("can_shop");
        if (lifeStage >= LifeStage.Adult)      agent.Tags.Add("can_work");
        if (lifeStage >= LifeStage.YoungChild) agent.Tags.Add("can_visit_park");

        if (lifeStage == LifeStage.Toddler)                                       agent.Tags.Add("eligible_for_preschool");
        if (lifeStage >= LifeStage.YoungChild && lifeStage <= LifeStage.Teen)     agent.Tags.Add("eligible_for_school");
        if (lifeStage == LifeStage.Baby || lifeStage == LifeStage.Toddler)        agent.Tags.Add("needs_parent_feed");

        agent.SetStat("life_stage", (float)lifeStage);
        agent.SetStat("age",        ageInYears);
    }

    public void Tick(AgentV2 _)      { }
    public void SlowTick(AgentV2 _)  { }
    public void Cleanup(AgentV2 _)   { }
}
