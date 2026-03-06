using System.Collections.Generic;

/// <summary>
/// Zone labels used to place agent dots inside an interior overlay.
/// </summary>
public enum InteriorZone
{
    Kitchen,
    Counter,
    Queue,
    Seating,
}

/// <summary>
/// Snapshot of one agent's current interior state, consumed by the UI overlay.
/// </summary>
public class InteriorAgentInfo
{
    public string       name;
    public InteriorZone zone;
    public bool         isWorker;
}

/// <summary>
/// Implement on any building that supports the interior view overlay.
/// GameUIManager checks for this interface when a building is clicked and,
/// if present, opens the schematic interior panel instead of the normal
/// inspection modal.
/// </summary>
public interface IHasInteriorView
{
    /// <summary>Display name shown in the interior overlay title bar.</summary>
    string InteriorDisplayName { get; }

    /// <summary>
    /// Returns a snapshot of every V2 agent currently inside the building
    /// and which zone they should be displayed in.
    /// </summary>
    List<InteriorAgentInfo> GetInteriorAgentInfo();
}
