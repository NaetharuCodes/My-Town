using System.Collections.Generic;
using UnityEngine;

// Abstract base for School and Preschool buildings.
// Municipal buildings run by the county — teachers are hired on shifts and paid from
// a daily county budget rather than from student fees. Students always attend for free.
// Subclasses (School, Preschool) are empty — the life stage check lives in Agent.
public abstract class SchoolBuilding : CommercialBuilding
{
    [Header("School Configuration")]
    public int maxStudents = 20;
    public int teachersPerClassroom = 2;
    public int studentsPerClassroom = 10;
    public int openHour = 8;
    public int closeHour = 15;

    [Header("Municipal Budget")]
    public int dailyMunicipalBudget = 200;
    public int startingTreasury = 2000;

    private readonly List<Agent> enrolledStudents = new List<Agent>();
    private readonly HashSet<Agent> presentStudents = new HashSet<Agent>();

    public int EnrolledCount => enrolledStudents.Count;
    public int PresentCount  => presentStudents.Count;

    public bool IsEnrollmentOpen() => enrolledStudents.Count < maxStudents;

    public bool IsInSession(int hour) => hour >= openHour && hour < closeHour;

    protected override void Awake()
    {
        base.Awake();
        treasury = startingTreasury;
    }

    protected override void Start()
    {
        base.Start();
        if (timeManager != null)
            timeManager.OnNewDay += ReceiveMunicipalBudget;
    }

    protected override void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnNewDay -= ReceiveMunicipalBudget;

        base.OnDestroy();

        // Unenroll all students so they seek a new school or revert to staying home.
        // Iterate over a copy because UnenrollSchool() may modify the list indirectly.
        foreach (Agent student in new List<Agent>(enrolledStudents))
            student.UnenrollSchool();
    }

    void ReceiveMunicipalBudget(int day)
    {
        treasury += dailyMunicipalBudget;
        Debug.Log($"{buildingName} received ${dailyMunicipalBudget} county budget. Treasury: ${treasury}");
    }

    protected override void SetupDefaultShifts()
    {
        int numClassrooms    = Mathf.CeilToInt((float)maxStudents / studentsPerClassroom);
        int requiredTeachers = numClassrooms * teachersPerClassroom;

        shifts.Add(new Shift
        {
            startHour       = openHour,
            durationHours   = closeHour - openHour,
            workersRequired = requiredTeachers,
            wage            = 350,
            payFrequency    = PayFrequency.Weekly
        });
    }

    // Returns true if the agent was successfully enrolled.
    public bool TryEnroll(Agent agent)
    {
        if (!IsEnrollmentOpen() || enrolledStudents.Contains(agent)) return false;
        enrolledStudents.Add(agent);
        return true;
    }

    public void UnenrollStudent(Agent agent)
    {
        enrolledStudents.Remove(agent);
        presentStudents.Remove(agent);
    }

    public void StudentArrive(Agent agent) => presentStudents.Add(agent);
    public void StudentLeave(Agent agent)  => presentStudents.Remove(agent);

    // Arriving at a school is always a success — the agent transitions to AtSchool.
    public override bool Interact(Agent agent)
    {
        StudentArrive(agent);
        return true;
    }
}
