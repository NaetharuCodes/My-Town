using UnityEngine;

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

    public void SetSpeed(float speed)
    {
        Time.timeScale = speed;
    }

    public float CurrentSpeed => Time.timeScale;

    private void Update()
    {
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

}