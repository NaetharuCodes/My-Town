using UnityEngine;

/// <summary>
/// Scene singleton. Holds references to all HealthCondition ScriptableObject assets so
/// agents can look up shared conditions without needing individual Inspector references.
/// Also exposes per-activity injury probabilities.
///
/// Place one instance in the scene and drag condition assets into the Inspector slots.
/// </summary>
public class HealthManager : MonoBehaviour
{
    public static HealthManager Instance { get; private set; }

    [Header("Condition Assets")]
    [Tooltip("Drag the CommonCold condition asset here.")]
    public HealthCondition commonCold;
    [Tooltip("Drag the Flu condition asset here.")]
    public HealthCondition flu;
    [Tooltip("Drag the MinorInjury condition asset here.")]
    public HealthCondition minorInjury;
    [Tooltip("Drag the SeriousInjury condition asset here.")]
    public HealthCondition seriousInjury;

    [Header("Injury Chance (per real second while active)")]
    [Tooltip("Chance per real second of a minor work injury while an agent is working.")]
    [Range(0f, 0.01f)]
    public float workInjuryChancePerSecond = 0.0005f;

    [Tooltip("Chance per real second of a minor injury while cooking.")]
    [Range(0f, 0.01f)]
    public float cookingInjuryChancePerSecond = 0.0003f;

    [Tooltip("Chance per real second of an injury while at the park.")]
    [Range(0f, 0.01f)]
    public float parkInjuryChancePerSecond = 0.0001f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
}
