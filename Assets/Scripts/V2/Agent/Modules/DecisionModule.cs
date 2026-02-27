using System;
using UnityEngine;

// Evaluates priorities every SlowTick (~1 s) and fires a "do_<action>" event when
// the best action changes.  All logic mirrors V1 Agent.HandleIdle() exactly.
//
// Priority order (highest → lowest):
//   0.  Baby / needs-parent-feed — stay home, no independent actions
//   1.  School (children: enrolled + in session)     ← mirrors work priority for adults
//   2.  Hospital (serious/critical illness)
//   3.  Work (adults: employed + shift active)
//   4.  Hungry — cook at home if possible, else eat out
//   5.  Doctor (mild illness — only after hunger satisfied)
//   6.  Go home (has home, not there yet)
//   7.  Seek home (homeless — find a dwelling or park)
//   8.  Seek groceries (pantry low, Teen+ can shop)
//   9.  Socialise (lonely, YoungChild+ can visit park)
//  10.  Seek job (unemployed adult)
//  11.  Seek school enrollment (eligible, not yet enrolled)
//  12.  Idle
public class DecisionModule : IAgentModule
{
    private string currentAction = "idle";

    public void Initialize(AgentV2 _) { }
    public void Tick(AgentV2 _)       { }
    public void Cleanup(AgentV2 _)    { }

    public void SlowTick(AgentV2 agent)
    {
        string best = Decide(agent);

        if (best != currentAction)
        {
            currentAction = best;
            agent.RaiseEvent("do_" + best);
            Debug.Log($"{agent.Name}: → {best}");
        }
    }

    // ── Priority cascade ───────────────────────────────────────────────────────
    private static string Decide(AgentV2 agent)
    {
        // ── P0: Baby — no independent actions ─────────────────────────────────
        if (agent.Tags.Contains("life_baby"))
            return "idle";

        // ── P0: Needs parent feeding (Baby/Toddler) — stay home ───────────────
        if (agent.Tags.Contains("needs_parent_feed"))
            return "idle";

        // ── P1: School (highest priority for eligible children in session) ─────
        if (agent.Tags.Contains("enrolled") && agent.Tags.Contains("school_hours")
            && IsSchoolEligible(agent))
            return "go_to_school";

        // ── P2: Medical emergency (serious/critical illness) ──────────────────
        if (agent.Tags.Contains("needs_hospital"))
            return "seek_hospital";

        // ── P3: Work (adults with an active shift) ────────────────────────────
        if (agent.Tags.Contains("employed") && agent.Tags.Contains("work_hours")
            && agent.Tags.Contains("can_work"))
            return "go_to_work";

        // ── P4: Hunger ────────────────────────────────────────────────────────
        if (agent.GetStat("hunger") >= 60f)
        {
            // Cook at home if capable and stocked; FoodModule handles the travel-then-cook flow.
            if (agent.Tags.Contains("can_cook") && agent.GetStat("pantry_groceries") > 0f)
                return "cook";

            // No groceries or can't cook — find a burger store.
            return "eat";
        }

        // ── P5: Mild illness (checked after hunger — matches V1 ordering) ──────
        if (agent.Tags.Contains("needs_doctor"))
            return "seek_doctor";

        // ── P6: Go home ───────────────────────────────────────────────────────
        if (agent.Tags.Contains("has_home") && !agent.Tags.Contains("at_home"))
            return "go_home";

        // ── P7: Seek housing (homeless) ───────────────────────────────────────
        if (!agent.Tags.Contains("has_home"))
            return "seek_home";

        // ── P8: Restock pantry (Teen+ can shop) ───────────────────────────────
        if (agent.Tags.Contains("can_shop") && agent.GetStat("pantry_groceries") < 2f)
            return "seek_groceries";

        // ── P9: Socialise (YoungChild+ when lonely) ───────────────────────────
        if (agent.Tags.Contains("can_visit_park") && agent.GetStat("loneliness") >= 60f)
            return "socialise";

        // ── P10: Seek work (unemployed adult) ─────────────────────────────────
        if (agent.Tags.Contains("can_work") && !agent.Tags.Contains("employed"))
            return "seek_work";

        // ── P11: Seek school enrollment ───────────────────────────────────────
        if (IsSchoolEligible(agent) && !agent.Tags.Contains("enrolled"))
            return "seek_school_enrollment";

        return "idle";
    }

    private static bool IsSchoolEligible(AgentV2 agent)
        => agent.Tags.Contains("eligible_for_school")
        || agent.Tags.Contains("eligible_for_preschool");
}
