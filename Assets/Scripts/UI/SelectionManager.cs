using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// Handles world-space click selection of buildings and agents.
///
/// Fires two sets of events:
///  - OnTileInspected(Building, List<Agent>) — composite event used by the inspection modal.
///    Always contains everything at the clicked cell regardless of priority.
///  - OnBuildingSelected / OnAgentSelected / OnSelectionCleared — kept for backward compat.
///
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
    /// <summary>Fires with everything at the clicked tile. Either argument may be null/empty.</summary>
    public event System.Action<Building, List<Agent>> OnTileInspected;

    // Kept for backward compat — the modal subscribes to OnTileInspected instead.
    public event System.Action<Building> OnBuildingSelected;
    public event System.Action<Agent>    OnAgentSelected;
    public event System.Action           OnSelectionCleared;

    private Camera       cam;
    private AgentManager agentManager;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        cam = Camera.main;
        if (buildingManager  == null) buildingManager  = FindAnyObjectByType<BuildingManager>();
        if (buildingPlacer   == null) buildingPlacer   = FindAnyObjectByType<BuildingPlacer>();
        if (agentManager     == null) agentManager     = FindAnyObjectByType<AgentManager>();

        // Borrow the buildings tilemap from BuildingManager — it already has the correct
        // reference assigned, avoiding any risk of accidentally grabbing the terrain tilemap.
        if (buildingsTilemap == null && buildingManager != null)
            buildingsTilemap = buildingManager.buildingsTilemap;
    }

    void Update()
    {
        // Suppress selection while the player is placing tiles or in delete mode
        if (buildingPlacer != null &&
            (buildingPlacer.CurrentSelectedTile != null || buildingPlacer.DeleteModeActive))
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Don't intercept clicks on UI elements
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector2 screenPos = Mouse.current.position.ReadValue();
        Vector3 worldPos  = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        worldPos.z = 0f;

        // ── Determine the grid cell under the cursor ──────────────────────────
        Vector3Int cell = buildingsTilemap != null
            ? buildingsTilemap.WorldToCell(worldPos)
            : Vector3Int.zero;

        Building building = buildingManager?.GetBuildingAt(cell);

        // ── Scan all agents in one pass ───────────────────────────────────────
        // Collect: (a) every agent in the same grid cell, (b) the nearest agent by proximity.
        var   agentsAtCell = new List<Agent>();
        Agent bestAgent    = null;
        float bestDist     = agentClickRadius;

        IReadOnlyList<Agent> allAgents = agentManager != null
            ? agentManager.GetAllAgents()
            : (IReadOnlyList<Agent>)FindObjectsByType<Agent>(FindObjectsSortMode.None);

        foreach (Agent agent in allAgents)
        {
            // Cell match for the composite event
            if (buildingsTilemap != null)
            {
                Vector3Int agentCell = buildingsTilemap.WorldToCell(agent.transform.position);
                if (agentCell == cell)
                    agentsAtCell.Add(agent);
            }

            // Proximity for the legacy single-selection events
            float d = Vector3.Distance(worldPos, agent.transform.position);
            if (d < bestDist) { bestDist = d; bestAgent = agent; }
        }

        // If the proximity agent wasn't caught by cell-matching, include them anyway
        if (bestAgent != null && !agentsAtCell.Contains(bestAgent))
            agentsAtCell.Add(bestAgent);

        // ── Fire events ───────────────────────────────────────────────────────
        if (building != null || agentsAtCell.Count > 0)
        {
            // Composite event — modal subscribes to this
            OnTileInspected?.Invoke(building, agentsAtCell);

            // Legacy single-selection
            if (bestAgent != null)
            {
                SelectedAgent    = bestAgent;
                SelectedBuilding = null;
                OnAgentSelected?.Invoke(bestAgent);
            }
            else if (building != null)
            {
                SelectedBuilding = building;
                SelectedAgent    = null;
                OnBuildingSelected?.Invoke(building);
            }
        }
        else
        {
            ClearSelection();
        }
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
}
