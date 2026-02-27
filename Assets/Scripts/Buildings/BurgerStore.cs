using UnityEngine;

public class BurgerStore : CommercialBuilding
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

    public override bool Interact(Agent agent)
    {
        if (!IsOpen())
        {
            Debug.Log($"{agent.agentName} tried to buy food but {buildingName} is closed.");
            return false;
        }

        if (!agent.TrySpend(mealPrice))
        {
            Debug.Log($"{agent.agentName} can't afford food!");
            return false;
        }

        agent.Feed(hungerRestored);
        treasury += mealPrice;
        return true;
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
}
