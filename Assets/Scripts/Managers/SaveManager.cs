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
            Vector3Int pos     = kvp.Key;
            Building   building = kvp.Value;

            BuildingSaveData bd = new BuildingSaveData
            {
                x        = pos.x,
                y        = pos.y,
                treasury = building.treasury
            };

            if (building is ResidentialBuilding res)
            {
                bd.type = "House";
                var (names, groceries) = res.GetSaveData();
                bd.occupantNames   = names;
                bd.pantryGroceries = groceries;
            }
            else if (building is BurgerStore)
            {
                bd.type = "BurgerStore";
                bd.shiftWorkerNames = ((CommercialBuilding)building).GetAssignedWorkerNames();
            }
            else if (building is Supermarket)
            {
                bd.type = "Supermarket";
                bd.shiftWorkerNames = ((CommercialBuilding)building).GetAssignedWorkerNames();
            }
            else if (building is Office)
            {
                bd.type = "Office";
                bd.shiftWorkerNames = ((CommercialBuilding)building).GetAssignedWorkerNames();
            }
            else if (building is Park)
            {
                bd.type = "Park";
            }
            else if (building is PoliceStation ps)
            {
                bd.type = "PoliceStation";
                bd.shiftWorkerNames = ps.GetAssignedWorkerNames();
            }

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

        // Agents
        foreach (Agent agent in agentManager.GetAllAgents())
        {
            agent.personality.GetSaveData(out List<string> traitKeys, out List<int> traitValues);
            AgentSaveData ad = new AgentSaveData
            {
                agentName        = agent.agentName,
                worldX           = agent.transform.position.x,
                worldY           = agent.transform.position.y,
                hunger           = agent.hunger,
                loneliness       = agent.loneliness,
                bankBalance      = agent.bankBalance,
                hasHome          = agent.hasHome,
                homeTileX        = agent.homeTile.x,
                homeTileY        = agent.homeTile.y,
                hasJob           = agent.hasJob,
                employerX        = agent.hasJob && agent.employer != null ? agent.employer.gridPosition.x : 0,
                employerY        = agent.hasJob && agent.employer != null ? agent.employer.gridPosition.y : 0,
                carriedGroceries = agent.CarriedCount(ItemType.Groceries),
                traitKeys        = traitKeys,
                traitValues      = traitValues
            };
            data.agents.Add(ad);
        }

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"Game saved. Day {data.time.currentDay}, {data.agents.Count} agents, {data.buildings.Count} buildings.");
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
        agentManager.DespawnAll();

        // --- Pass 1: rebuild tilemap + building objects ---
        Dictionary<Vector3Int, BuildingSaveData> buildingDataByPos = new();

        foreach (BuildingSaveData bd in data.buildings)
        {
            Vector3Int pos = new Vector3Int(bd.x, bd.y, 0);

            TileBase tile = TileForType(bd.type);
            if (tile != null)
                buildingsTilemap.SetTile(pos, tile);

            if (bd.type == "Road") continue; // roads need no building object

            Building b = CreateBuilding(bd.type, pos);
            if (b != null)
            {
                b.treasury = bd.treasury;
                buildingManager.RegisterBuilding(b);
                buildingDataByPos[pos] = bd;
            }
        }

        // --- Pass 2: recreate agents ---
        Dictionary<string, Agent> agentsByName = new();
        foreach (AgentSaveData ad in data.agents)
        {
            Agent agent = agentManager.SpawnAgentFromSave(ad);
            agent.hunger      = ad.hunger;
            agent.loneliness  = ad.loneliness;
            agent.bankBalance = ad.bankBalance;
            agent.personality.LoadFromSave(ad.traitKeys, ad.traitValues);
            if (ad.carriedGroceries > 0)
                agent.AddToInventory(ItemType.Groceries, ad.carriedGroceries);
            agentsByName[ad.agentName] = agent;
        }

        // --- Pass 3: re-link agents to buildings ---
        foreach (AgentSaveData ad in data.agents)
        {
            if (!agentsByName.TryGetValue(ad.agentName, out Agent agent)) continue;

            // Restore home
            if (ad.hasHome)
            {
                Vector3Int homePos = new Vector3Int(ad.homeTileX, ad.homeTileY, 0);
                Building homeBuilding = buildingManager.GetBuildingAt(homePos);
                if (homeBuilding is ResidentialBuilding res && res.DwellingUnits.Count > 0)
                {
                    DwellingUnit unit = res.DwellingUnits[0];
                    unit.DwellingOccupancy.Add(agent);
                    // Restore pantry from the saved building data
                    if (buildingDataByPos.TryGetValue(homePos, out BuildingSaveData bd))
                        unit.pantry.Add(ItemType.Groceries, bd.pantryGroceries);
                    agent.AssignHome(homePos, unit);
                }
            }

            // Restore job
            if (ad.hasJob)
            {
                Vector3Int empPos = new Vector3Int(ad.employerX, ad.employerY, 0);
                Building empBuilding = buildingManager.GetBuildingAt(empPos);
                if (empBuilding is CommercialBuilding commercial)
                {
                    Shift shift = commercial.TryHire(agent);
                    if (shift != null)
                    {
                        agent.employer      = commercial;
                        agent.assignedShift = shift;
                        agent.hasJob        = true;
                    }
                }
            }
        }

        Debug.Log($"Game loaded. Day {data.time.currentDay}, {data.agents.Count} agents restored.");
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
            default:
                return null;
        }
    }
}
