using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class BuildingPlacer : MonoBehaviour
{
    [Header("Tilemaps")]
    public Tilemap terrainTilemap;
    public Tilemap buildingsTilemap;

    [Header("Tile Assets")]
    public TileBase roadTile;
    public TileBase houseTile;
    public TileBase burgerStoreTile;

    [Header("References")]
    public BuildingManager buildingManager;

    private TileBase selectedTile;
    private Camera cam;
    private Vector3Int lastDragCell = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SelectTile(roadTile, "Road");
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SelectTile(houseTile, "House");
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            SelectTile(burgerStoreTile, "Burger Store");
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SelectTile(null, "None");

        if (Mouse.current.leftButton.isPressed && selectedTile != null)
        {
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

    void SelectTile(TileBase tile, string name)
    {
        selectedTile = tile;
    }

    void PlaceTile(Vector3Int cellPos)
    {
        TileBase terrainAtPos = terrainTilemap.GetTile(cellPos);
        TileBase buildingAtPos = buildingsTilemap.GetTile(cellPos);

        if (terrainAtPos == null)
            return;

        if (terrainAtPos.name == "Water")
        {
            Debug.Log("Can't build on water!");
            return;
        }

        if (buildingAtPos != null)
        {
            Debug.Log("Something is already built here!");
            return;
        }

        buildingsTilemap.SetTile(cellPos, selectedTile);

        // Register with BuildingManager (roads aren't buildings)
        if (selectedTile == houseTile)
        {
            ResidentialBuilding building = new GameObject("House").AddComponent<ResidentialBuilding>();
            building.gridPosition = cellPos;
            DwellingUnit unit = new DwellingUnit();
            building.DwellingUnits.Add(unit);
            buildingManager.RegisterBuilding(building);
        }
        else if (selectedTile == burgerStoreTile)
        {
            BurgerStore store = new GameObject("BurgerStore").AddComponent<BurgerStore>();
            store.gridPosition = cellPos;
            buildingManager.RegisterBuilding(store);
        }
    }
}