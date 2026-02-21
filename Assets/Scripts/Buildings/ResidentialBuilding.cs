using System.Collections.Generic;
using UnityEngine;

public class ResidentialBuilding : Building
{
    public List<DwellingUnit> DwellingUnits = new List<DwellingUnit>();

    private TimeManager timeManager;

    void Start()
    {
        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
            timeManager.OnNewDay += CollectRent;
    }

    void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnNewDay -= CollectRent;
    }

    void CollectRent(int day)
    {
        foreach (DwellingUnit unit in DwellingUnits)
        {
            foreach (Agent agent in unit.DwellingOccupancy)
            {
                agent.ChargeRent(unit.rentPerDay);
                treasury += unit.rentPerDay;
            }
        }
    }

    public override bool Interact(Agent agent)
    {
        foreach (DwellingUnit unit in DwellingUnits)
        {
            if (unit.DwellingOccupancy.Count == 0)
            {
                unit.DwellingOccupancy.Add(agent);
                agent.AssignHome(gridPosition, unit);
                return true;
            }
        }

        return false;
    }

    // Returns occupant names and pantry count for the first dwelling unit.
    public (List<string> occupantNames, int pantryGroceries) GetSaveData()
    {
        if (DwellingUnits.Count == 0)
            return (new List<string>(), 0);
        DwellingUnit unit = DwellingUnits[0];
        var names = new List<string>();
        foreach (Agent a in unit.DwellingOccupancy)
            names.Add(a.agentName);
        return (names, unit.pantry.Get(ItemType.Groceries));
    }

    public bool HasVacantUnit()
    {
        foreach (DwellingUnit unit in DwellingUnits)
        {
            if (unit.DwellingOccupancy.Count == 0)
                return true;
        }
        return false;
    }

}