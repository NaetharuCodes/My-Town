using System.Collections.Generic;
using UnityEngine;

public class BurgerStore : CommercialBuilding, IHasInteriorView
{
    [Header("Burger Store")]
    public int mealPrice = 10;
    public float hungerRestored = 100f;

    protected override void SetupDefaultShifts()
    {
        // Random start between 7am and 2pm so burger stores open at different times.
        int startHour = Random.Range(7, 15);
        shifts.Add(new Shift
        {
            startHour = startHour,
            durationHours = 8,
            workersRequired = 1,
            wage = 280,
            payFrequency = PayFrequency.Weekly
        });
    }

    public override bool Interact(AgentV2 agent)
    {
        if (!IsOpen())
        {
            Debug.Log($"{agent.Name} tried to buy food but {buildingName} is closed.");
            return false;
        }

        if (agent.GetStat("bank_balance") < mealPrice)
        {
            Debug.Log($"{agent.Name} can't afford food!");
            return false;
        }

        agent.ModifyStat("bank_balance", -mealPrice);
        agent.GetModule<FoodModule>()?.RestoreHunger(agent, hungerRestored);
        treasury += mealPrice;
        return true;
    }

    // ── IHasInteriorView ──────────────────────────────────────────────────────

    public string InteriorDisplayName => buildingName;

    public List<InteriorAgentInfo> GetInteriorAgentInfo()
    {
        var result = new List<InteriorAgentInfo>();

        foreach (AgentV2 agent in GetPresentV2Agents())
        {
            var work     = agent.GetModule<WorkModule>();
            var food     = agent.GetModule<FoodModule>();
            bool isWorker = work != null && work.Employer == this;

            InteriorZone zone;
            if (isWorker)
                zone = work.IsCurrentlyWorking ? InteriorZone.Kitchen : InteriorZone.Counter;
            else if (food != null && food.IsEating)
                zone = InteriorZone.Seating;
            else
                zone = InteriorZone.Queue;

            result.Add(new InteriorAgentInfo
            {
                name     = agent.Name,
                zone     = zone,
                isWorker = isWorker,
            });
        }

        return result;
    }
}
