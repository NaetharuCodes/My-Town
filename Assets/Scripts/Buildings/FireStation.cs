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

    // Find any on-duty firefighter not already responding and send them to the burning building.
    public void DispatchFirefighter(Building burningBuilding)
    {
        foreach (var shift in shifts)
        {
            foreach (var worker in shift.PresentWorkers)
            {
                if (worker.currentState != AgentState.RespondingToFire &&
                    worker.currentState != AgentState.Extinguishing)
                {
                    worker.AssignFireResponse(burningBuilding);
                    Debug.Log($"{buildingName} dispatched {worker.agentName} to fire at {burningBuilding.buildingName}!");
                    return;
                }
            }
        }
        Debug.Log($"No available firefighters at {buildingName} to respond to fire at {burningBuilding.buildingName}!");
    }

    public override bool Interact(Agent agent) => false;
}
