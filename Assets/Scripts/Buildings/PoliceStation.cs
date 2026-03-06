using UnityEngine;

// A municipal commercial building. Officers are regular agents hired here on shifts.
// Treasury is topped up by a daily municipal budget rather than earned from sales,
// so wages are always covered regardless of crime rates.
public class PoliceStation : CommercialBuilding
{
    [Header("Police")]
    public int arrestFine = 100;

    [Header("Municipal Budget")]
    public int dailyMunicipalBudget = 600; // deposited into treasury each in-game day
    public int startingTreasury = 5000;    // initial public funding

    protected override void Awake()
    {
        base.Awake(); // let CommercialBuilding call SetupDefaultShifts first
        treasury = startingTreasury;
        buildingName = "Police Station";
    }

    protected override void SetupDefaultShifts()
    {
        // Day shift: 7am – 7pm, 2 officers
        shifts.Add(new Shift
        {
            startHour      = 7,
            durationHours  = 12,
            workersRequired = 2,
            wage           = 450,
            payFrequency   = PayFrequency.Weekly
        });

        // Night shift: 7pm – 7am, 1 officer (crosses midnight)
        shifts.Add(new Shift
        {
            startHour      = 19,
            durationHours  = 12,
            workersRequired = 1,
            wage           = 450,
            payFrequency   = PayFrequency.Weekly
        });
    }

    protected override void Start()
    {
        base.Start(); // wire up timeManager + payroll events
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

    // TODO: V2 police dispatch — send an on-duty V2 officer to chase the criminal.
    public void DispatchOfficer(AgentV2 criminal)
    {
        Debug.Log($"Police dispatch for {criminal.Name} — V2 officer dispatch not yet implemented.");
    }
}
