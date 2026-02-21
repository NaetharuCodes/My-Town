using System.Collections.Generic;
using UnityEngine;

// Holds a collection of named traits that influence agent behaviour.
// Trait values are integers; higher means a stronger version of the trait.
// Examples: { "thief": 2 }, { "lazy": 1 }
[System.Serializable]
public class Personality
{
    private Dictionary<string, int> traits = new Dictionary<string, int>();

    public bool HasTrait(string trait) => traits.ContainsKey(trait);
    public int GetTrait(string trait) => traits.TryGetValue(trait, out int v) ? v : 0;
    public void SetTrait(string trait, int value) => traits[trait] = value;

    // Returns true if a random roll succeeds for the given trait.
    // Each trait level adds 25% success chance: level 1 = 25%, level 2 = 50%, level 3 = 75% (cap 90%).
    public bool RollTrait(string trait)
    {
        int value = GetTrait(trait);
        if (value <= 0) return false;
        float successChance = Mathf.Clamp(value * 0.25f, 0f, 0.9f);
        return Random.value < successChance;
    }

    // --- Save / load support ---
    // Dictionary iteration order is consistent within a session, so parallel lists are safe.
    public void GetSaveData(out List<string> keys, out List<int> values)
    {
        keys = new List<string>(traits.Keys);
        values = new List<int>(traits.Values);
    }

    public void LoadFromSave(List<string> keys, List<int> values)
    {
        traits.Clear();
        if (keys == null || values == null) return;
        int count = Mathf.Min(keys.Count, values.Count);
        for (int i = 0; i < count; i++)
            traits[keys[i]] = values[i];
    }

    public override string ToString()
    {
        if (traits.Count == 0) return "no traits";
        var parts = new List<string>();
        foreach (var kv in traits)
            parts.Add($"{kv.Key}:{kv.Value}");
        return string.Join(", ", parts);
    }
}
