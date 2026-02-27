using System;
using UnityEngine;

// Manages school enrollment and attendance.
//
// Blackboard tags written:
//   enrolled       → agent is enrolled at a school/preschool
//   school_hours   → enrolled school is currently in session
//
// Events consumed:
//   do_seek_school_enrollment  → find and enroll at nearest eligible school
//   do_go_to_school            → navigate to school; check in on arrival
//   arrived                    → check in at school when CurrentTask == "school_travel"
//
// Events raised:
//   move_to     → via LocomotionModule
//   do_go_home  → when school session ends
public class SchoolModule : IAgentModule
{
    // ── Enrollment state ───────────────────────────────────────────────────────
    public bool          IsEnrolled    { get; private set; } = false;
    public SchoolBuilding EnrolledSchool { get; private set; }

    private bool isAtSchool      = false;
    private int  lastCheckedHour = -1;

    private Action<string, object> eventHandler;
    private Action<int>            onHourChanged;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_seek_school_enrollment": HandleDoSeekEnrollment(agent); break;
                case "do_go_to_school":           HandleDoGoToSchool(agent);     break;
                case "arrived":                   HandleArrived(agent, data);    break;
            }

            if (evt.StartsWith("do_") && evt != "do_go_to_school"
                                      && evt != "do_seek_school_enrollment")
                CancelTravelIntent(agent);
        };
        agent.OnEvent += eventHandler;

        if (agent.TimeManager != null)
        {
            onHourChanged = hour =>
            {
                lastCheckedHour = hour;
                UpdateSchoolHoursTag(agent);
                CheckSessionEnd(agent);
            };
            agent.TimeManager.OnHourChanged += onHourChanged;
            lastCheckedHour = agent.TimeManager.CurrentHour;
        }
    }

    public void Tick(AgentV2 _)     { }
    public void SlowTick(AgentV2 agent) => UpdateSchoolHoursTag(agent);

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
        if (agent.TimeManager != null && onHourChanged != null)
            agent.TimeManager.OnHourChanged -= onHourChanged;
    }

    // ── Public API (called on school demolition) ───────────────────────────────
    public void Unenroll(AgentV2 agent)
    {
        if (!IsEnrolled) return;
        EnrolledSchool?.UnenrollStudent(agent);
        EnrolledSchool = null;
        IsEnrolled     = false;
        isAtSchool     = false;
        agent.Tags.Remove("enrolled");
        agent.Tags.Remove("school_hours");

        if (agent.CurrentTask is "school_travel" or "school")
            agent.CurrentTask = "";
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoSeekEnrollment(AgentV2 agent)
    {
        if (IsEnrolled || agent.BuildingManager == null) return;

        Vector3Int from = agent.BuildingsTilemap.WorldToCell(agent.transform.position);
        Vector3Int? tile;

        if (agent.Tags.Contains("eligible_for_preschool"))
        {
            tile = agent.BuildingManager.FindNearest<Preschool>(from, s => s.IsEnrollmentOpen());
        }
        else if (agent.Tags.Contains("eligible_for_school"))
        {
            tile = agent.BuildingManager.FindNearest<School>(from, s => s.IsEnrollmentOpen());
        }
        else return;

        if (tile == null) return;

        var school = agent.BuildingManager.GetBuildingAt(tile.Value) as SchoolBuilding;
        if (school == null) return;

        if (!school.TryEnroll(agent)) return;
        EnrolledSchool = school;
        IsEnrolled     = true;
        agent.Tags.Add("enrolled");
        UpdateSchoolHoursTag(agent);
        Debug.Log($"{agent.Name}: enrolled at {school.name}");
    }

    private void HandleDoGoToSchool(AgentV2 agent)
    {
        if (!IsEnrolled || EnrolledSchool == null) return;
        if (isAtSchool) return;

        agent.CurrentTask = "school_travel";
        agent.RaiseEvent("move_to", EnrolledSchool.transform.position);
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "school_travel") return;

        EnrolledSchool.StudentArrive(agent);
        isAtSchool = true;
        agent.CurrentTask = "school";
        Debug.Log($"{agent.Name}: arrived at school");
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask == "school_travel")
            agent.CurrentTask = "";
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private void UpdateSchoolHoursTag(AgentV2 agent)
    {
        bool inSession = IsEnrolled && EnrolledSchool != null
                         && EnrolledSchool.IsInSession(lastCheckedHour);

        if (inSession) agent.Tags.Add("school_hours");
        else           agent.Tags.Remove("school_hours");
    }

    private void CheckSessionEnd(AgentV2 agent)
    {
        if (!isAtSchool || EnrolledSchool == null) return;
        if (EnrolledSchool.IsInSession(lastCheckedHour)) return;

        // Session ended — leave school and go home.
        EnrolledSchool.StudentLeave(agent);
        isAtSchool = false;
        agent.CurrentTask = "";
        agent.Tags.Remove("at_school");

        if (agent.GetModule<HomeModule>()?.HasHome == true)
            agent.RaiseEvent("do_go_home");
    }
}
