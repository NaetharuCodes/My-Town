using UnityEngine;

public class Supermarket : CommercialBuilding
{
    [Header("Supermarket")]
    public int groceryPackCost = 25;
    public int groceriesPerPack = 4;

    protected override void SetupDefaultShifts()
    {
        // Supermarkets open earlier and stay open longer than fast food.
        // Random start between 6am and 9am for variety across the town.
        int startHour = Random.Range(6, 10);
        shifts.Add(new Shift
        {
            startHour = startHour,
            durationHours = 14,
            workersRequired = 1,
            wage = 350,
            payFrequency = PayFrequency.Weekly
        });
    }

    public override bool Interact(AgentV2 agent)
    {
        if (!IsOpen())
        {
            Debug.Log($"{agent.Name} tried to shop but {buildingName} is closed.");
            return false;
        }

        if (agent.GetStat("bank_balance") < groceryPackCost)
        {
            Debug.Log($"{agent.Name} can't afford groceries.");
            return false;
        }

        agent.ModifyStat("bank_balance", -groceryPackCost);
        agent.GetModule<HomeModule>()?.AddCarriedGroceries(groceriesPerPack);
        treasury += groceryPackCost;
        Debug.Log($"{agent.Name} bought {groceriesPerPack} groceries.");
        return true;
        // TODO: thief trait logic not yet implemented for V2 agents.
    }
}
