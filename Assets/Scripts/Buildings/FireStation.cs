using UnityEngine;

// A municipal commercial building. Firefighters are regular agents hired here on shifts.
// Treasury is topped up by a daily municipal budget.
// When any building catches fire it calls FireStation.DispatchFirefighter() on the nearest station.
public class FireStation : CommercialBuilding
{
    [Header("Municipal Budget")]
    public int dailyMunicipalBudget = 500;
    public int startingTreasury = 5000;

    protected override void Awake()
    {
        base.Awake();
        treasury = startingTreasury;
        buildingName = "Fire Station";
    }

    protected override void SetupDefaultShifts()
    {
        // Day shift: 8am – 8pm, 2 firefighters
        shifts.Add(new Shift
        {
            startHour       = 8,
            durationHours   = 12,
            workersRequired = 2,
            wage            = 420,
            payFrequency    = PayFrequency.Weekly
        });

        // Night shift: 8pm – 8am, 1 firefighter (crosses midnight)
        shifts.Add(new Shift
        {
            startHour       = 20,
            durationHours   = 12,
            workersRequired = 1,
            wage            = 420,
            payFrequency    = PayFrequency.Weekly
        });
    }

    protected override void Start()
    {
        base.Start();
        if (timeManager != null)
            timeManager.OnNewDay += ReceiveMunicipalBudget;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (timeManager != null)
            timeManager.OnNewDay -= ReceiveMunicipalBudget;
    }

    void ReceiveMunicipalBudget(int day)
    {
        treasury += dailyMunicipalBudget;
        Debug.Log($"{buildingName} received ${dailyMunicipalBudget} municipal budget. Treasury: ${treasury}");
    }

    // TODO: V2 fire dispatch — send an on-duty V2 firefighter to respond.
    public void DispatchFirefighter(Building burningBuilding)
    {
        Debug.Log($"Fire at {burningBuilding.buildingName} — V2 firefighter dispatch not yet implemented.");
    }
}
