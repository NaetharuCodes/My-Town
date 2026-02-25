using UnityEngine;
using System.Collections.Generic;

public class FriendEdge
{
    public string AgentIdA;
    public string AgentIdB;
    public int OpinionAtoB;
    public int OpinionBtoA;
    public bool IsAlive;

}

public class FriendManagerV2 : MonoBehaviour
{
    public static FriendManagerV2 instance;

    // Dual index - same edge referenced from both sides
    private Dictionary<string, List<FriendEdge>> byAgentA = new();
    private Dictionary<string, List<FriendEdge>> byAgentB = new();

    public int MaxFriends = 5;

    private void Awake()
    {
        instance = this;
    }

    public void AddEdge(string agentA, string agentB, int opinionAtoB, int opinionBtoA, bool isAlive = true)
    {

        if (GetFriendCount(agentA) >= MaxFriends || GetFriendCount(agentB) >= MaxFriends)
        {
            Debug.Log("Unable to add friend as already at max friend number");
            return;
        }

        var edge = new FriendEdge
        {
            AgentIdA = agentA,
            AgentIdB = agentB,
            OpinionAtoB = opinionAtoB,
            OpinionBtoA = opinionBtoA,
            IsAlive = isAlive
        };

        // Same object in both directions
        if (!byAgentA.ContainsKey(agentA))
            byAgentA[agentA] = new List<FriendEdge>();
        byAgentA[agentA].Add(edge);

        if (!byAgentB.ContainsKey(agentB))
            byAgentB[agentB] = new List<FriendEdge>();
        byAgentB[agentB].Add(edge);
    }

    // Check both sides of graph as Agent may be in position A or B
    public List<FriendEdge> GetFriends(string agentId)
    {
        var friends = new List<FriendEdge>();
        friends.AddRange(byAgentA.GetValueOrDefault(agentId, new()));
        friends.AddRange(byAgentB.GetValueOrDefault(agentId, new()));
        return friends;
    }

    private int GetFriendCount(string agentId)
    {
        int count = 0;
        if (byAgentA.TryGetValue(agentId, out var listA)) count += listA.Count;
        if (byAgentB.TryGetValue(agentId, out var listB)) count += listB.Count;
        return count;
    }
}