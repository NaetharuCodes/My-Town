using System.Collections.Generic;
using UnityEngine;

public class ResidentialBuilding : Building
{
    public List<DwellingUnit> DwellingUnits = new List<DwellingUnit>();

    public bool HasVacantUnit()
    {
        foreach (DwellingUnit unit in DwellingUnits)
            if (unit.DwellingOccupancyV2.Count == 0) return true;
        return false;
    }

    // Finds the best vacant unit for a family of the given size.
    // Prefers a unit with enough bedrooms; falls back to any vacant unit.
    public DwellingUnit FindBestVacantUnit(int familySize)
    {
        DwellingUnit fallback = null;
        foreach (DwellingUnit unit in DwellingUnits)
        {
            if (unit.DwellingOccupancyV2.Count > 0) continue;
            if (unit.NumberOfBedrooms >= familySize) return unit;
            if (fallback == null) fallback = unit;
        }
        return fallback;
    }
}