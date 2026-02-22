using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// Handles world-space click selection of buildings and agents.
///
/// Fires events that other systems (GameUIManager) subscribe to.
/// Selection is suppressed while BuildingPlacer has an active tile selected.
/// </summary>
public class SelectionManager : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public Tilemap        buildingsTilemap;
    public BuildingManager buildingManager;
    public BuildingPlacer  buildingPlacer;

    /// <summary>World-unit radius within which a click counts as hitting an agent.</summary>
    public float agentClickRadius = 0.8f;

    // ── Current selection ─────────────────────────────────────────────────────
    public Building SelectedBuilding { get; private set; }
    public Agent    SelectedAgent    { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public event System.Action<Building> OnBuildingSelected;
    public event System.Action<Agent>    OnAgentSelected;
    public event System.Action           OnSelectionCleared;

    private Camera cam;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        cam = Camera.main;
        if (buildingManager  == null) buildingManager  = FindAnyObjectByType<BuildingManager>();
        if (buildingPlacer   == null) buildingPlacer   = FindAnyObjectByType<BuildingPlacer>();

        // Borrow the buildings tilemap from BuildingManager — it already has the correct
        // reference assigned, avoiding any risk of accidentally grabbing the terrain tilemap.
        if (buildingsTilemap == null && buildingManager != null)
            buildingsTilemap = buildingManager.buildingsTilemap;
    }

    void Update()
    {
        // Suppress selection while the player is placing tiles
        if (buildingPlacer != null && buildingPlacer.CurrentSelectedTile != null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Don't intercept clicks on UI elements
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos  = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        // ── 1. Agent proximity check (agents live in world space, not on a tilemap) ──
        Agent    bestAgent = null;
        float    bestDist  = agentClickRadius;

        foreach (Agent agent in FindObjectsByType<Agent>(FindObjectsSortMode.None))
        {
            float d = Vector3.Distance(worldPos, agent.transform.position);
            if (d < bestDist)
            {
                bestDist  = d;
                bestAgent = agent;
            }
        }

        if (bestAgent != null)
        {
            SelectAgent(bestAgent);
            return;
        }

        // ── 2. Building tilemap cell lookup ───────────────────────────────────
        if (buildingsTilemap != null && buildingManager != null)
        {
            Vector3Int cell     = buildingsTilemap.WorldToCell(worldPos);
            Building   building = buildingManager.GetBuildingAt(cell);
            if (building != null)
            {
                SelectBuilding(building);
                return;
            }
        }

        // ── 3. Empty space – clear ────────────────────────────────────────────
        ClearSelection();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public void ClearSelection()
    {
        SelectedBuilding = null;
        SelectedAgent    = null;
        OnSelectionCleared?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────────────────

    void SelectAgent(Agent agent)
    {
        SelectedBuilding = null;
        SelectedAgent    = agent;
        OnAgentSelected?.Invoke(agent);
    }

    void SelectBuilding(Building building)
    {
        SelectedAgent    = null;
        SelectedBuilding = building;
        OnBuildingSelected?.Invoke(building);
    }
}
