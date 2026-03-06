using System;
using System.Collections.Generic;

public class Family
{
    public string familyId;   // GUID — used to reconnect members on save/load
    public string familyName; // shared surname
    public FamilyType familyType;
    public List<AgentV2> membersV2 = new List<AgentV2>();

    public Family(string familyName, FamilyType type)
    {
        this.familyName = familyName;
        this.familyType = type;
        familyId = Guid.NewGuid().ToString();
    }

    public void AddMemberV2(AgentV2 agent)
    {
        membersV2.Add(agent);
    }
}
