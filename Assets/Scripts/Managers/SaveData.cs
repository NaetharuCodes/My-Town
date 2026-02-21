using System.Collections.Generic;

// Plain serialisable data classes — no MonoBehaviour, no Unity references.
// JsonUtility can serialise these directly to/from JSON.

[System.Serializable]
public class SaveData
{
    public TimeSaveData time = new TimeSaveData();
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    public List<AgentSaveData> agents = new List<AgentSaveData>();
}

[System.Serializable]
public class TimeSaveData
{
    public int currentDay;
    public int currentHour;
}

[System.Serializable]
public class BuildingSaveData
{
    // "House", "BurgerStore", "Supermarket", "Office", "Park", "PoliceStation", "Road"
    public string type;
    public int x;
    public int y;
    public int treasury;

    // Residential only
    public List<string> occupantNames = new List<string>();
    public int pantryGroceries;

    // Commercial only — first shift's assigned workers
    public List<string> shiftWorkerNames = new List<string>();
}

[System.Serializable]
public class AgentSaveData
{
    public string agentName;
    public float worldX;
    public float worldY;
    public float hunger;
    public float loneliness;
    public int bankBalance;
    public bool hasHome;
    public int homeTileX;
    public int homeTileY;
    public bool hasJob;
    public int employerX;
    public int employerY;
    public int carriedGroceries;

    // Personality traits — stored as parallel lists for JsonUtility compatibility.
    public List<string> traitKeys   = new List<string>();
    public List<int>    traitValues = new List<int>();
}
