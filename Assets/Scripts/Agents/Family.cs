using System;
using System.Collections.Generic;

public class Family
{
    public string familyId;   // GUID — used to reconnect members on save/load
    public string familyName; // shared surname
    public FamilyType familyType;
    public List<Agent> members = new List<Agent>();

    public Family(string familyName, FamilyType type)
    {
        this.familyName = familyName;
        this.familyType = type;
        familyId = Guid.NewGuid().ToString();
    }

    public void AddMember(Agent agent)
    {
        members.Add(agent);
    }
}
