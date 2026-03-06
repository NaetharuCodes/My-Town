using System.Collections.Generic;
using UnityEngine;

// Abstract base for School and Preschool buildings.
// Municipal buildings run by the county — teachers are hired on shifts and paid from
// a daily county budget rather than from student fees. Students always attend for free.
// Subclasses (School, Preschool) are empty — the life stage check lives in Agent/SchoolModule.
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

    private readonly List<AgentV2>   enrolledStudentsV2 = new List<AgentV2>();
    private readonly HashSet<AgentV2> presentStudentsV2 = new HashSet<AgentV2>();

    public int EnrolledCount => enrolledStudentsV2.Count;
    public int PresentCount  => presentStudentsV2.Count;

    public bool IsEnrollmentOpen() => EnrolledCount < maxStudents;
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

        foreach (AgentV2 student in new List<AgentV2>(enrolledStudentsV2))
            student.GetModule<SchoolModule>()?.Unenroll(student);
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

    public bool TryEnroll(AgentV2 agent)
    {
        if (!IsEnrollmentOpen() || enrolledStudentsV2.Contains(agent)) return false;
        enrolledStudentsV2.Add(agent);
        return true;
    }

    public void UnenrollStudent(AgentV2 agent)
    {
        enrolledStudentsV2.Remove(agent);
        presentStudentsV2.Remove(agent);
    }

    public void StudentArrive(AgentV2 agent) => presentStudentsV2.Add(agent);
    public void StudentLeave(AgentV2 agent)  => presentStudentsV2.Remove(agent);

    public override bool Interact(AgentV2 agent)
    {
        StudentArrive(agent);
        return true;
    }
}
