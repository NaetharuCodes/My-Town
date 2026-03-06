using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// V2 Agent


public class AgentV2 : MonoBehaviour
{
    public string Id { get; private set; }
    public string Name;

    public Dictionary<string, float> Stats { get; private set; }
    public HashSet<string> Tags { get; private set; }

    // Tracks what the agent is currently doing — used by modules to coordinate travel/arrival.
    // Set by the module that takes ownership of movement; cleared on task completion.
    public string CurrentTask { get; set; } = "";

    // Scene-level dependencies — set via Initialise() before adding modules.
    public BuildingManager BuildingManager { get; private set; }
    public TimeManager TimeManager { get; private set; }
    public Pathfinder Pathfinder { get; private set; }
    public Tilemap BuildingsTilemap { get; private set; }

    public event Action<string, object> OnEvent;

    public void RaiseEvent(string eventType, object data = null)
    {
        OnEvent?.Invoke(eventType, data);
    }

    // Call this before adding any modules so they can access scene references.
    public void Initialise(BuildingManager bm, Pathfinder pf, Tilemap tilemap, TimeManager tm)
    {
        BuildingManager = bm;
        Pathfinder = pf;
        BuildingsTilemap = tilemap;
        TimeManager = tm;
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

    private void Start()
    {
        if (AgentScheduler.Instance == null)
        {
            var go = new GameObject("AgentScheduler");
            go.AddComponent<AgentScheduler>();
            Debug.LogWarning("AgentV2: AgentScheduler was missing from scene — created automatically. Add it manually for Inspector configuration.");
        }
        AgentScheduler.Instance.Register(this);
    }

    private void OnDestroy()
    {
        AgentScheduler.Instance?.Unregister(this);
    }

    private void Update()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].Tick(this);
        }
    }

    // Called by AgentScheduler, staggered across frames.
    public void SlowTick()
    {
        for (int i = 0; i < modules.Count; i++)
        {
            modules[i].SlowTick(this);
        }
    }

    public float GetStat(string key, float defaultValue = 0f)
    {
        return Stats.TryGetValue(key, out float value) ? value : defaultValue;
    }

    public void SetStat(string key, float value)
    {
        Stats[key] = value;
    }

    public void ModifyStat(string key, float amount, float min = float.MinValue, float max = float.MaxValue)
    {
        float current = GetStat(key);
        Stats[key] = Mathf.Clamp(current + amount, min, max);
    }
}