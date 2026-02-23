// ArrivalPoint marks where new residents enter the town — a bus stop, ferry port, etc.
// Place one at the edge of the road network. ArrivalManager will find it automatically
// and spawn arriving families here.
public class ArrivalPoint : Building
{
    public override bool Interact(Agent agent) => false; // residents don't interact with it directly
}
