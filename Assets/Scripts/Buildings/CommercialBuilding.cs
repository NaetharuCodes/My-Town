using UnityEngine;

// Base class for all commercial/service buildings (shops, restaurants, etc.)
// Subclasses define what service they provide via Interact().
public class CommercialBuilding : Building
{
    public override bool Interact(Agent agent)
    {
        return false;
    }
}
