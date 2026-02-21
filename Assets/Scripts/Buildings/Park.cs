// Parks are municipal buildings — owned by the city, free to use, always open.
// No workers or shifts required. Agents visit to restore their social need.
// Future: costs (maintenance) flow to the municipality budget rather than a private owner.
public class Park : Building
{
    public bool isMunicipal = true;

    public override bool Interact(Agent agent) => true;
}
