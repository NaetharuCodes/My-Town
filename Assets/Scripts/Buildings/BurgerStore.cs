using UnityEngine;

public class BurgerStore : CommercialBuilding
{
    [Header("Burger Store")]
    public int mealPrice = 10;
    public float hungerRestored = 100f;

    public override bool Interact(Agent agent)
    {
        if (!agent.TrySpend(mealPrice))
        {
            Debug.Log($"{agent.agentName} can't afford food!");
            return false;
        }

        agent.Feed(hungerRestored);
        return true;
    }
}
