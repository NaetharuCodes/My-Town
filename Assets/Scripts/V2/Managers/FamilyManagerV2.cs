using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class FamilyEdge
{
    public string AgentIdA;
    public string AgentIdB;
    public string Type;
    public bool IsAlive;
}

public class FamilyManagerV2 : MonoBehaviour
{
    public static FamilyManagerV2 instance;

    // Dual index - same edge referenced from both sides
    private Dictionary<string, List<FamilyEdge>> byAgentA = new();
    private Dictionary<string, List<FamilyEdge>> byAgentB = new();

    private void Awake()
    {
        instance = this;
    }

    public void AddEdge(string agentA, string agentB, string type)
    {
        var edge = new FamilyEdge
        {
            AgentIdA = agentA,
            AgentIdB = agentB,
            Type = type,
            IsAlive = true
        };

        // Same object in both directions
        if (!byAgentA.ContainsKey(agentA))
            byAgentA[agentA] = new List<FamilyEdge>();
        byAgentA[agentA].Add(edge);

        if (!byAgentB.ContainsKey(agentB))
            byAgentB[agentB] = new List<FamilyEdge>();
        byAgentB[agentB].Add(edge);
    }

    public List<FamilyEdge> GetChildren(string agentId)
    {
        return byAgentA.GetValueOrDefault(agentId, new())
            .Where(e => e.Type == "father_of" || e.Type == "mother_of")
            .ToList();
    }

    public List<FamilyEdge> GetParents(string agentId)
    {
        return byAgentB.GetValueOrDefault(agentId, new())
            .Where(e => e.Type == "father_of" || e.Type == "mother_of")
            .ToList();
    }

    public FamilyEdge GetSpouse(string agentId)
    {
        // Could be on either edge so need to check A and B

        var spouse = byAgentA.GetValueOrDefault(agentId, new())
        .FirstOrDefault(e => e.Type == "spouse_of");

        if (spouse != null) return spouse;

        return byAgentB.GetValueOrDefault(agentId, new())
        .FirstOrDefault(e => e.Type == "spouse_of");
    }

    public List<string> GetSiblings(string agentId)
    {
        var sibilings = new HashSet<string>();
        var parents = GetParents(agentId);

        foreach (var parentEdge in parents)
        {
            var children = GetChildren(parentEdge.AgentIdA);

            foreach (var child in children)
            {
                if (child.AgentIdA != agentId)
                {
                    sibilings.Add(child.AgentIdB);
                }
            }
        }
        return sibilings.ToList();
    }

    public bool AreRelated(string agentIdX, string agentIdY)
    {
        // Check for parent/child/spouse
        var directRelatives = GetAllFamily(agentIdX);
        if (directRelatives.Contains(agentIdY)) return true;

        // Check for siblings
        var siblings = GetSiblings(agentIdX);
        if (siblings.Contains(agentIdY)) return true;

        // Check cousins
        foreach (var person in siblings)
        {
            var cousins = GetChildren(person);
            if (cousins.Any(e => e.AgentIdB == agentIdY)) return true;
        }

        return false;
    }

    public List<string> GetAllFamily(string agentId)
    {
        var family = new HashSet<string>();

        foreach (var edge in byAgentA.GetValueOrDefault(agentId, new()))
        {
            family.Add(edge.AgentIdB);
        }

        foreach (var edge in byAgentB.GetValueOrDefault(agentId, new()))
        {
            family.Add(edge.AgentIdA);
        }

        return family.ToList();
    }

    public void MarkDeceased(string agentId)
    {
        foreach (var edge in byAgentA.GetValueOrDefault(agentId, new()))
            edge.IsAlive = false;
        foreach (var edge in byAgentB.GetValueOrDefault(agentId, new()))
            edge.IsAlive = false;
    }
}

