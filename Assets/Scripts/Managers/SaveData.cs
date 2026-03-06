using System.Collections.Generic;

// Plain serialisable data classes — no MonoBehaviour, no Unity references.
// JsonUtility can serialise these directly to/from JSON.

[System.Serializable]
public class SaveData
{
    public TimeSaveData time = new TimeSaveData();
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    // TODO: V2 agent save data
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
    // "House", "BurgerStore", "Supermarket", "Office", "Park", "PoliceStation", "FireStation", "Road"
    public string type;
    public int x;
    public int y;
    public int treasury;
}
