using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// Handles world-space click selection of buildings.
/// Selection is suppressed while BuildingPlacer has an active tile selected.
/// </summary>
public class SelectionManager : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public Tilemap         buildingsTilemap;
    public BuildingManager buildingManager;
    public BuildingPlacer  buildingPlacer;

    // ── Current selection ─────────────────────────────────────────────────────
    public Building SelectedBuilding { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action<Building> OnTileInspected;
    public event System.Action<Building> OnBuildingSelected;
    public event System.Action           OnSelectionCleared;

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (buildingManager == null) buildingManager = FindAnyObjectByType<BuildingManager>();
        if (buildingPlacer  == null) buildingPlacer  = FindAnyObjectByType<BuildingPlacer>();

        if (buildingsTilemap == null && buildingManager != null)
            buildingsTilemap = buildingManager.buildingsTilemap;
    }

    void Update()
    {
        if (buildingPlacer != null &&
            (buildingPlacer.CurrentSelectedTile != null || buildingPlacer.DeleteModeActive))
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos  = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        Vector3Int cell     = buildingsTilemap != null ? buildingsTilemap.WorldToCell(worldPos) : Vector3Int.zero;
        Building   building = buildingManager?.GetBuildingAt(cell);

        if (building != null)
        {
            SelectedBuilding = building;
            OnTileInspected?.Invoke(building);
            OnBuildingSelected?.Invoke(building);
        }
        else
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        SelectedBuilding = null;
        OnSelectionCleared?.Invoke();
    }
}
