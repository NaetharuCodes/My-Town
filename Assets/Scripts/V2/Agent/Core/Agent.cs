using System;
using System.Collection.Generic;
using UnityEngine;

public class Agent : MonoBehaviour
{
    public string Id { get; private set; }
    public string Name;

    public Dictionary<string, float> Stats { get; private set; }

    public HashSet<string> Tags { get; private set; }

    public event Action<string, object> OnEvent;

    public void RaiseEvent(string eventType, object data = null)
    {
        OnEvent?.Invoke(eventType, data);
    }

    private List<IAgentModule> modules;

    private void Awake()
    {
        Id = System.Guid.NewGuid().ToString();
        Stats = new Dictionary<string, float>();
        Tags = new HashSet<string>();
        modules = new List<IAgentModule>();
    }

    public void AddModule(IAgentModule module)
    {
        modules.Add(module);
        module.Initialize(this);
    }

    public void RemoveModule(IAgentModule module)
    {
        module.Cleanup(this);
        modules.Remove(module);
    }

    public bool HasModule<T>() where T : IAgentModule
    {
        foreach (var module in modules)
        {
            if (module is T) return true;
        }
        return false;
    }

    public T GetModule<T>() where T : class, IAgentModule
    {
        foreach (var module in modules)
        {
            if (module is T typed) return typed;
        }

        return null;
    }

    private void Update()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].Tick(this);
        }
    }

    public float GetStat(string key, float defaultValue = 0f)
    {
        return Stats.TryGetValue(key, out float value) ? value : defaultValue;
    }

    public void setStat(string key, float value)
    {
        Stats[key] = value;
    }

    public void ModifyStat(string key, float amount, float min = float.MinValue, float max = float.MaxValue)
    {
        float current = GetStat(key);
        Stats[key] = Mathf.Clamp(current + amount, min, max);
    }
}