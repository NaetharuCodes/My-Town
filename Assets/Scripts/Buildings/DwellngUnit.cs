// A Dwelling Unity is a single unit of housing. For cases like tower
// Blocks this may be different from a single house.
// Each dwelling unit houses a single family or group of related people.

using System.Collections.Generic;

public class DwellingUnit
{
    public int NumberOfBedrooms;
    public int NumberofBathrooms;

    public int InteriorCondition;

    public int rentPerDay = 40;

    public List<AgentV2> DwellingOccupancyV2 = new List<AgentV2>();

    // Food and goods stored at home. Stocked by grocery runs; consumed when cooking.
    public Inventory pantry = new Inventory();

}