using UnityEngine;

/// <summary>
/// ScriptableObject defining a disease or injury. Create one asset per condition
/// via Assets > Create > Health > Condition and configure in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "Health/Condition", fileName = "NewCondition")]
public class HealthCondition : ScriptableObject
{
    [Header("Identity")]
    public string conditionName;
    public ConditionSeverity severity;

    [Header("Contagion")]
    public bool isContagious;
    [Tooltip("World-space radius within which this condition can spread.")]
    public float spreadRadius = 2f;
    [Tooltip("Chance (0–1) of spreading to a nearby agent each DiseaseCheckInterval.")]
    public float spreadChancePerExposure = 0.05f;

    [Header("Progression")]
    [Tooltip("In-game days before the agent shows symptoms.")]
    public float incubationDays = 1f;
    [Tooltip("Chance (0–1) per in-game day of recovering without treatment.")]
    [Range(0f, 1f)]
    public float naturalRecoveryChancePerDay = 0.1f;
    [Tooltip("If true, the condition worsens if the agent receives no treatment.")]
    public bool progressesIfUntreated = false;
    [Tooltip("Days symptomatic before the condition worsens (if progressesIfUntreated).")]
    public float daysUntilProgression = 5f;
    [Tooltip("The condition this becomes when it progresses. Leave null if the condition is already Critical — the agent will die.")]
    public HealthCondition progressionCondition;
}

public enum ConditionSeverity
{
    Mild,
    Serious,
    Critical
}
