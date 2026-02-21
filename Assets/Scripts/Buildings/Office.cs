using UnityEngine;

// A generic office building — provides jobs but has no customer interaction.
// Workers arrive for their shift and leave when it ends; no Interact() needed.
public class Office : CommercialBuilding
{
    protected override void SetupDefaultShifts()
    {
        // Standard office hours with slight variation across buildings.
        int startHour = Random.Range(8, 10);
        shifts.Add(new Shift
        {
            startHour    = startHour,
            durationHours = 8,
            workersRequired = 3,
            wage          = 400,
            payFrequency  = PayFrequency.Weekly
        });
    }
}
