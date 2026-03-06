using System;
using UnityEngine;

// Tracks tiredness and drives the daily sleep cycle.
//
// Blackboard stats written:
//   tiredness  (0–100)  — accumulates each game hour awake; restores while sleeping
//
// Blackboard tags written:
//   needs_sleep  → agent should seek sleep (in sleep window or exhausted)
//   is_sleeping  → agent is currently asleep
//
// Events consumed:
//   do_sleep  → navigate home and sleep; if homeless, sleep in place
//               (TODO: heatmap-guided rest-spot search for homeless agents)
//   arrived   → begin sleeping when CurrentTask == "sleep_travel"
//
// Events raised:
//   move_to  → via LocomotionModule
//
// Public API:
//   SleepHour, WakeHour
//   SetSchedule(sleepHour, wakeHour)  ← call when a work/school shift demands a different pattern
public class SleepModule : IAgentModule
{
    // ── Sleep schedule (adjustable) ────────────────────────────────────────────
    public int SleepHour { get; private set; } = 23;   // default: 11 pm
    public int WakeHour  { get; private set; } = 6;    // default: 6 am

    // ── Config ─────────────────────────────────────────────────────────────────
    private const float TirednessPerHour   = 5f;    // +5/hr awake  → hits 80 after ~16 hrs
    private const float RestorePerHour     = 10f;   // −10/hr asleep → clears in ~8 hrs
    private const float EmergencyThreshold = 92f;   // force sleep even outside window
    private const int   MinSleepHours      = 6;
    private const int   MaxSleepHours      = 10;    // hard cap — never oversleep

    // ── State ──────────────────────────────────────────────────────────────────
    private bool isSleeping = false;
    private int  hoursSlept = 0;
    private int  lastHour   = -1;

    private Action<string, object> eventHandler;
    private Action<int>            onHourChanged;

    // ── IAgentModule ───────────────────────────────────────────────────────────
    public void Initialize(AgentV2 agent)
    {
        agent.SetStat("tiredness", 0f);

        eventHandler = (evt, data) =>
        {
            switch (evt)
            {
                case "do_sleep": HandleDoSleep(agent);      break;
                case "arrived":  HandleArrived(agent, data); break;
                case "path_failed":
                    // Can't path home to sleep — sleep in place instead.
                    if (agent.CurrentTask == "sleep_travel")
                        BeginSleep(agent);
                    break;
            }
            if (evt.StartsWith("do_") && evt != "do_sleep")
                CancelTravelIntent(agent);
        };
        agent.OnEvent += eventHandler;

        if (agent.TimeManager != null)
        {
            onHourChanged = hour =>
            {
                lastHour = hour;
                if (isSleeping)
                    TickSleep(agent, hour);
                else
                    agent.ModifyStat("tiredness", TirednessPerHour, 0f, 100f);

                UpdateSleepTag(agent, hour);
            };
            agent.TimeManager.OnHourChanged += onHourChanged;
            lastHour = agent.TimeManager.CurrentHour;
            UpdateSleepTag(agent, lastHour);
        }
    }

    public void Tick(AgentV2 _)       { }
    public void SlowTick(AgentV2 agent) => UpdateSleepTag(agent, lastHour);

    public void Cleanup(AgentV2 agent)
    {
        agent.OnEvent -= eventHandler;
        if (agent.TimeManager != null && onHourChanged != null)
            agent.TimeManager.OnHourChanged -= onHourChanged;
    }

    // ── Public API ─────────────────────────────────────────────────────────────
    // Shift sleep window to suit a non-standard work or school schedule.
    // Examples:
    //   Night shift worker:  SetSchedule(7, 15)  — sleeps 7 am to 3 pm
    //   Early-morning shift: SetSchedule(21, 5)  — sleeps 9 pm to 5 am
    public void SetSchedule(int sleepHour, int wakeHour)
    {
        SleepHour = sleepHour;
        WakeHour  = wakeHour;
    }

    // ── Event handlers ─────────────────────────────────────────────────────────
    private void HandleDoSleep(AgentV2 agent)
    {
        if (isSleeping) return;

        var home = agent.GetModule<HomeModule>();
        if (home != null && home.HasHome)
        {
            if (agent.Tags.Contains("at_home"))
            {
                BeginSleep(agent);
            }
            else
            {
                agent.CurrentTask = "sleep_travel";
                agent.RaiseEvent("move_to", home.HomeWorldPosition);
            }
            return;
        }

        // TODO: heatmap-guided rest-spot search (park bench, shelter, etc.).
        // For now, sleep wherever the agent stands.
        BeginSleep(agent);
    }

    private void HandleArrived(AgentV2 agent, object _)
    {
        if (agent.CurrentTask != "sleep_travel") return;
        BeginSleep(agent);
    }

    private void CancelTravelIntent(AgentV2 agent)
    {
        if (agent.CurrentTask == "sleep_travel")
            agent.CurrentTask = "";
    }

    // ── Sleep lifecycle ─────────────────────────────────────────────────────────
    private void BeginSleep(AgentV2 agent)
    {
        isSleeping        = true;
        hoursSlept        = 0;
        agent.CurrentTask = "sleeping";
        agent.Tags.Add("is_sleeping");
        Debug.Log($"{agent.Name}: fell asleep (window {SleepHour}–{WakeHour})");
    }

    private void TickSleep(AgentV2 agent, int hour)
    {
        hoursSlept++;
        agent.ModifyStat("tiredness", -RestorePerHour, 0f, 100f);

        bool restedEnough  = hoursSlept >= MinSleepHours;
        bool outsideWindow = !IsInSleepWindow(hour);
        bool hitCap        = hoursSlept >= MaxSleepHours;

        if ((restedEnough && outsideWindow) || hitCap)
            WakeUp(agent);
    }

    private void WakeUp(AgentV2 agent)
    {
        isSleeping = false;
        hoursSlept = 0;
        agent.SetStat("tiredness", 0f);
        agent.CurrentTask = "";
        agent.Tags.Remove("is_sleeping");
        agent.Tags.Remove("needs_sleep");
        Debug.Log($"{agent.Name}: woke up after {hoursSlept} hrs");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private void UpdateSleepTag(AgentV2 agent, int hour)
    {
        bool inWindow  = IsInSleepWindow(hour);
        bool exhausted = agent.GetStat("tiredness") >= EmergencyThreshold;

        if (inWindow || exhausted)
            agent.Tags.Add("needs_sleep");
        else if (!isSleeping)
            agent.Tags.Remove("needs_sleep");
    }

    // True when hour falls inside the sleep window (handles midnight wrap).
    private bool IsInSleepWindow(int hour)
    {
        if (SleepHour < WakeHour)   // e.g., 2–6 (no midnight crossing)
            return hour >= SleepHour && hour < WakeHour;
        else                         // e.g., 23–7 (crosses midnight)
            return hour >= SleepHour || hour < WakeHour;
    }
}
