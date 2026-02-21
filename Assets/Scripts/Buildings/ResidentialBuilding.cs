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
                agent.AssignHome(gridPosition);
                return true;
            }
        }

        return false;
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