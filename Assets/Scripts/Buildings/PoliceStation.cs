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

    // Find any on-duty officer who isn't already chasing someone and set them on the criminal.
    public void DispatchOfficer(Agent criminal)
    {
        foreach (var shift in shifts)
        {
            foreach (var officer in shift.PresentWorkers)
            {
                if (officer.currentState != AgentState.Chasing)
                {
                    officer.AssignChase(criminal, arrestFine);
                    return;
                }
            }
        }
        Debug.Log($"No available officers at {buildingName} to respond!");
    }

    // Police station is not interactive by regular agents.
    public override bool Interact(Agent agent) => false;
}
