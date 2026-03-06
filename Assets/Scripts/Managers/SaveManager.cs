using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Handles saving and loading the full game state to a single JSON file.
// Autosaves once per in-game day. Sits on a GameObject in the Game scene.
//
// Inspector references required:
//   buildingManager, agentManager, buildingPlacer,
//   terrainTilemap (not saved — just needed for context), buildingsTilemap
public class SaveManager : MonoBehaviour
{
    // Set by MainMenu before loading the scene to control new-game vs load.
    public static bool NewGameRequested = true;

    [Header("References")]
    public BuildingManager buildingManager;
    public AgentManager agentManager;
    public BuildingPlacer buildingPlacer;
    public Tilemap buildingsTilemap;

    private TimeManager timeManager;
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool SaveExists() => File.Exists(SavePath);

    void Start()
    {
        timeManager = FindFirstObjectByType<TimeManager>();
        if (timeManager != null)
            timeManager.OnNewDay += OnNewDay;

        if (!NewGameRequested && SaveExists())
            Load();
    }

    void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnNewDay -= OnNewDay;
    }

    void OnNewDay(int day) => Save();

    void Update()
    {
        // Ctrl+S — manual save shortcut until the in-game UI is built.
        if (Keyboard.current.sKey.wasPressedThisFrame &&
            (Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCtrlKey.isPressed))
        {
            Save();
            Debug.Log("Manual save triggered.");
        }
    }

    // -----------------------------------------------------------------------
    // SAVE
    // -----------------------------------------------------------------------

    public void Save()
    {
        SaveData data = new SaveData();

        // Time
        if (timeManager != null)
        {
            data.time.currentDay  = timeManager.CurrentDay;
            data.time.currentHour = timeManager.CurrentHour;
        }

        // Buildings tilemap tiles + building objects
        foreach (var kvp in buildingManager.GetAllBuildings())
        {
            Vector3Int pos      = kvp.Key;
            Building   building = kvp.Value;

            BuildingSaveData bd = new BuildingSaveData
            {
                x        = pos.x,
                y        = pos.y,
                treasury = building.treasury,
                type     = building switch
                {
                    ResidentialBuilding => "House",
                    BurgerStore         => "BurgerStore",
                    Supermarket         => "Supermarket",
                    Office              => "Office",
                    Park                => "Park",
                    PoliceStation       => "PoliceStation",
                    FireStation         => "FireStation",
                    _                   => "Unknown"
                }
            };

            data.buildings.Add(bd);
        }

        // Road tiles — iterate the tilemap for any tile not covered by a building object
        BoundsInt bounds = buildingsTilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = buildingsTilemap.GetTile(pos);
            if (tile == null) continue;
            if (tile.name == "Road")
            {
                data.buildings.Add(new BuildingSaveData
                {
                    type = "Road",
                    x    = pos.x,
                    y    = pos.y
                });
            }
        }

        // TODO: V2 agent save — save V2 agent state (home, job, stats) for each AgentV2.

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"Game saved. Day {data.time.currentDay}, {data.buildings.Count} buildings.");
    }

    // -----------------------------------------------------------------------
    // LOAD
    // -----------------------------------------------------------------------

    public void Load()
    {
        if (!SaveExists())
        {
            Debug.LogWarning("No save file found.");
            return;
        }

        SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));

        // --- Restore time ---
        if (timeManager != null)
            timeManager.SetTime(data.time.currentDay, data.time.currentHour);

        // --- Clear current state ---
        buildingsTilemap.ClearAllTiles();
        buildingManager.ClearAll();
        // TODO: despawn V2 agents when V2 save/load is implemented.

        // --- Pass 1: rebuild tilemap + building objects ---
        foreach (BuildingSaveData bd in data.buildings)
        {
            Vector3Int pos = new Vector3Int(bd.x, bd.y, 0);

            TileBase tile = TileForType(bd.type);
            if (tile != null)
                buildingsTilemap.SetTile(pos, tile);

            if (bd.type == "Road") continue;

            Building b = CreateBuilding(bd.type, pos);
            if (b != null)
            {
                b.treasury = bd.treasury;
                buildingManager.RegisterBuilding(b);
            }
        }

        // TODO: V2 agent load — restore V2 agent state (home, job, stats) from save data.

        Debug.Log($"Game loaded. Day {data.time.currentDay}, {data.buildings.Count} buildings restored.");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    TileBase TileForType(string type) => type switch
    {
        "Road"          => buildingPlacer.roadTile,
        "House"         => buildingPlacer.houseTile,
        "BurgerStore"   => buildingPlacer.burgerStoreTile,
        "Supermarket"   => buildingPlacer.supermarketTile,
        "Office"        => buildingPlacer.officeTile,
        "Park"          => buildingPlacer.parkTile,
        "PoliceStation" => buildingPlacer.policeStationTile,
        "FireStation"   => buildingPlacer.fireStationTile,
        _               => null
    };

    Building CreateBuilding(string type, Vector3Int pos)
    {
        switch (type)
        {
            case "House":
            {
                ResidentialBuilding b = new GameObject("House").AddComponent<ResidentialBuilding>();
                b.gridPosition = pos;
                b.DwellingUnits.Add(new DwellingUnit());
                return b;
            }
            case "BurgerStore":
            {
                BurgerStore b = new GameObject("BurgerStore").AddComponent<BurgerStore>();
                b.gridPosition = pos;
                return b;
            }
            case "Supermarket":
            {
                Supermarket b = new GameObject("Supermarket").AddComponent<Supermarket>();
                b.gridPosition = pos;
                return b;
            }
            case "Office":
            {
                Office b = new GameObject("Office").AddComponent<Office>();
                b.gridPosition = pos;
                return b;
            }
            case "Park":
            {
                Park b = new GameObject("Park").AddComponent<Park>();
                b.gridPosition = pos;
                return b;
            }
            case "PoliceStation":
            {
                PoliceStation b = new GameObject("PoliceStation").AddComponent<PoliceStation>();
                b.gridPosition = pos;
                return b;
            }
            case "FireStation":
            {
                FireStation b = new GameObject("FireStation").AddComponent<FireStation>();
                b.gridPosition = pos;
                return b;
            }
            default:
                return null;
        }
    }
}
