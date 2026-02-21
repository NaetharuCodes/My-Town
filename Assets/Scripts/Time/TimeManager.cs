using UnityEngine;
using UnityEngine.InputSystem;

public class TimeManager : MonoBehaviour
{
    [Header("Time Settings")]
    public float realSecondsPerGameHour = 15f;

    // Current Time States
    private float timeAccumulator = 0f;
    private int currentHour = 6;
    private int currentDay = 1;

    public event System.Action<int> OnHourChanged;
    public event System.Action<int> OnNewDay;
    public event System.Action OnNewWeek;

    public int CurrentHour => currentHour;
    public int CurrentDay => currentDay;

    public void SetTime(int day, int hour)
    {
        currentDay = day;
        currentHour = hour;
        timeAccumulator = 0f;
    }

    public string TimeString => $"Day {currentDay} - {currentHour:00}:00";

    private void Update()
    {
        ControlSpeed();

        timeAccumulator += Time.deltaTime;
        if (timeAccumulator > realSecondsPerGameHour)
        {
            timeAccumulator -= realSecondsPerGameHour;
            currentHour += 1;

            if (currentHour > 23)
            {
                currentDay += 1;
                currentHour = 0;
                OnNewDay?.Invoke(currentDay);
                if (currentDay % 7 == 0)
                    OnNewWeek?.Invoke();
            }

            OnHourChanged?.Invoke(currentHour);
        }
    }

    private void ControlSpeed()
    {
        if (Keyboard.current.pKey.wasPressedThisFrame)
        {
            Time.timeScale = 0;
        }

        if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            Time.timeScale = 1;
        }

        if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            Time.timeScale = 5;
        }

        if (Keyboard.current.digit0Key.wasPressedThisFrame)
        {
            Time.timeScale = 10;
        }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 30), TimeString);
    }
}