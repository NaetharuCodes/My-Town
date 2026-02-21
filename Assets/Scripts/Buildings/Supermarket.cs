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

    public override bool Interact(Agent agent)
    {
        if (!IsOpen())
        {
            Debug.Log($"{agent.agentName} tried to shop but {buildingName} is closed.");
            return false;
        }

        if (!agent.TrySpend(groceryPackCost))
        {
            Debug.Log($"{agent.agentName} can't afford groceries.");
            return false;
        }

        agent.AddToInventory(ItemType.Groceries, groceriesPerPack);
        treasury += groceryPackCost;
        Debug.Log($"{agent.agentName} bought {groceriesPerPack} groceries. Now carrying: {agent.CarriedCount(ItemType.Groceries)}");
        return true;
    }
}
