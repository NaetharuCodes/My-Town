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

        // Thief trait: roll to steal instead of paying.
        if (agent.personality.HasTrait("thief") && agent.personality.RollTrait("thief"))
        {
            agent.AddToInventory(ItemType.Groceries, groceriesPerPack);
            EventLog.LogDanger($"{agent.agentName} stole from {buildingName}!");
            Debug.Log($"{agent.agentName} STOLE {groceriesPerPack} groceries from {buildingName}!");

            // 40% chance of being caught regardless of thief level.
            if (Random.value < 0.4f)
            {
                EventLog.LogDanger($"{agent.agentName} was caught stealing – police alerted!");
                Debug.Log($"{agent.agentName} was caught stealing! Alerting police...");
                AlertPolice(agent);
            }
            return true;
        }

        // Normal purchase.
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

    void AlertPolice(Agent criminal)
    {
        if (criminal.isBeingChased) return; // already being pursued

        PoliceStation station = FindFirstObjectByType<PoliceStation>();
        if (station != null)
            station.DispatchOfficer(criminal);
        else
            EventLog.LogWarning($"No police in town — {criminal.agentName} gets away!");
            Debug.Log($"No police station in town — {criminal.agentName} gets away!");
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
