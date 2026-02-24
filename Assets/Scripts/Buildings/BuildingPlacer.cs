using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap terrainTilemap;
    public Tilemap buildingsTilemap;

    [Header("Tile Assets")]
    public TileBase roadTile;
    public TileBase houseTile;
    public TileBase burgerStoreTile;
    public TileBase supermarketTile;
    public TileBase officeTile;
    public TileBase parkTile;
    public TileBase policeStationTile;
    public TileBase fireStationTile;
    public TileBase arrivalPointTile;
    public TileBase schoolTile;
    public TileBase preschoolTile;

    [Header("References")]
    public BuildingManager buildingManager;

    private TileBase selectedTile;
    private Camera cam;
    private Vector3Int lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

    public bool DeleteModeActive { get; private set; }

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (selectedTile != null)
                SelectTile(null, "None");
            else if (DeleteModeActive)
                SetDeleteMode(false);
            else
                SceneManager.LoadScene("MainMenu");
        }

        // Delete mode click
        if (DeleteModeActive && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector3 worldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            worldPos.z = 0f;
            DeleteTileAt(buildingsTilemap.WorldToCell(worldPos));
            return;
        }

        if (Mouse.current.leftButton.isPressed && selectedTile != null)
        {
            // Don't place tiles when the mouse is over a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                return;
            }

            Vector3 worldPos = cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            Vector3Int cellPos = buildingsTilemap.WorldToCell(worldPos);

            bool isDrag = selectedTile == roadTile;
            if (!isDrag && !Mouse.current.leftButton.wasPressedThisFrame)
                return;

            if (cellPos != lastDragCell)
            {
                lastDragCell = cellPos;
                PlaceTile(cellPos);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
    }

    public TileBase CurrentSelectedTile => selectedTile;

    public void SelectTile(TileBase tile, string name)
    {
        selectedTile = tile;
    }

    public void SetDeleteMode(bool active)
    {
        DeleteModeActive = active;
        if (active) SelectTile(null, "None"); // clear any placed-tile selection
    }

    void DeleteTileAt(Vector3Int cellPos)
    {
        Building building = buildingManager.GetBuildingAt(cellPos);
        if (building == null)
        {
            // May be a road or other bare tile with no Building component
            if (buildingsTilemap.GetTile(cellPos) != null)
            {
                buildingsTilemap.SetTile(cellPos, null);
                EventLog.Log("Demolished road.");
            }
            return;
        }

        // Notify residents/workers before the building is destroyed
        if (building is ResidentialBuilding res)
        {
            foreach (var unit in res.DwellingUnits)
                foreach (Agent a in new List<Agent>(unit.DwellingOccupancy))
                    a.ClearHome();
        }
        else if (building is CommercialBuilding com)
        {
            foreach (var shift in com.shifts)
                foreach (Agent a in new List<Agent>(shift.AssignedWorkers))
                    a.LoseJob();
        }

        string name = building.buildingName;
        buildingManager.DeregisterBuilding(cellPos);
        buildingsTilemap.SetTile(cellPos, null);
        Destroy(building.gameObject);

        EventLog.Log($"Demolished {name}.");
    }

    void PlaceTile(Vector3Int cellPos)
    {
        TileBase terrainAtPos = terrainTilemap.GetTile(cellPos);
        TileBase buildingAtPos = buildingsTilemap.GetTile(cellPos);

        if (terrainAtPos == null)
            return;

        if (terrainAtPos.name == "Water")
        {
            EventLog.Log("Can't build on water.");
            return;
        }

        if (buildingAtPos != null)
        {
            EventLog.Log("Something is already built here.");
            return;
        }

        buildingsTilemap.SetTile(cellPos, selectedTile);

        // Register with BuildingManager (roads aren't buildings)
        if (selectedTile == houseTile)
        {
            ResidentialBuilding building = new GameObject("House").AddComponent<ResidentialBuilding>();
            building.gridPosition = cellPos;
            DwellingUnit unit = new DwellingUnit();
            unit.NumberOfBedrooms = Random.Range(2, 6); // 2–5 bedrooms for natural family size variety
            building.DwellingUnits.Add(unit);
            buildingManager.RegisterBuilding(building);
        }
        else if (selectedTile == burgerStoreTile)
        {
            BurgerStore store = new GameObject("BurgerStore").AddComponent<BurgerStore>();
            store.gridPosition = cellPos;
            buildingManager.RegisterBuilding(store);
        }
        else if (selectedTile == supermarketTile)
        {
            Supermarket market = new GameObject("Supermarket").AddComponent<Supermarket>();
            market.gridPosition = cellPos;
            buildingManager.RegisterBuilding(market);
        }
        else if (selectedTile == officeTile)
        {
            Office office = new GameObject("Office").AddComponent<Office>();
            office.gridPosition = cellPos;
            buildingManager.RegisterBuilding(office);
        }
        else if (selectedTile == parkTile)
        {
            Park park = new GameObject("Park").AddComponent<Park>();
            park.gridPosition = cellPos;
            buildingManager.RegisterBuilding(park);
        }
        else if (selectedTile == policeStationTile)
        {
            PoliceStation station = new GameObject("PoliceStation").AddComponent<PoliceStation>();
            station.gridPosition = cellPos;
            buildingManager.RegisterBuilding(station);
        }
        else if (selectedTile == fireStationTile)
        {
            FireStation station = new GameObject("FireStation").AddComponent<FireStation>();
            station.gridPosition = cellPos;
            buildingManager.RegisterBuilding(station);
        }
        else if (selectedTile == arrivalPointTile)
        {
            ArrivalPoint point = new GameObject("ArrivalPoint").AddComponent<ArrivalPoint>();
            point.gridPosition = cellPos;
            buildingManager.RegisterBuilding(point);
        }
        else if (selectedTile == schoolTile)
        {
            School school = new GameObject("School").AddComponent<School>();
            school.gridPosition = cellPos;
            school.buildingName = "School";
            buildingManager.RegisterBuilding(school);
        }
        else if (selectedTile == preschoolTile)
        {
            Preschool preschool = new GameObject("Preschool").AddComponent<Preschool>();
            preschool.gridPosition = cellPos;
            preschool.buildingName = "Preschool";
            buildingManager.RegisterBuilding(preschool);
        }
    }
}