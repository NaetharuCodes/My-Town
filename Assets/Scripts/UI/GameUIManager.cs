using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using UnityEngine.EventSystems;

/// <summary>
/// Builds and manages the in-game HUD entirely in code – no scene prefabs required.
///
/// Setup: add this component to any GameObject in the Game scene.
/// References to TimeManager and BuildingPlacer are found automatically;
/// you can also assign them explicitly in the Inspector.
///
/// Optionally drag tile sprites into the "Building Icons" slots so the
/// build panel shows thumbnails. Leave them empty for text-only buttons.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    [Header("References (auto-found if left empty)")]
    public TimeManager    timeManager;
    public BuildingPlacer buildingPlacer;
    public SelectionManager selectionManager;

    [Header("Building Icons (optional – drag tile sprites here)")]
    public Sprite roadIcon;
    public Sprite houseIcon;
    public Sprite burgerStoreIcon;
    public Sprite supermarketIcon;
    public Sprite officeIcon;
    public Sprite parkIcon;
    public Sprite policeStationIcon;
    public Sprite fireStationIcon;

    // ── live UI references ────────────────────────────────────────────────────
    private Text       timeText;
    private GameObject buildPanel;
    private Button[]   speedButtons;
    private Button[]   buildButtons;
    private int        activeBuildIdx = -1;

    // Build mode
    private Button     buildToggleButton;

    // Info panel
    private GameObject infoPanel;
    private Text       infoPanelTitle;
    private Text       infoPanelContent;

    // ── colour palette ────────────────────────────────────────────────────────
    private static readonly Color PanelBg    = new Color(0.10f, 0.10f, 0.12f, 0.92f);
    private static readonly Color BtnNormal  = new Color(0.20f, 0.22f, 0.25f, 1.00f);
    private static readonly Color BtnActive  = new Color(0.18f, 0.48f, 0.78f, 1.00f);
    private static readonly Color BtnHover   = new Color(0.30f, 0.33f, 0.38f, 1.00f);
    private static readonly Color BtnPressed = new Color(0.12f, 0.36f, 0.60f, 1.00f);
    private static readonly Color White      = Color.white;

    private Font defaultFont;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void Start()
    {
        if (timeManager      == null) timeManager      = FindAnyObjectByType<TimeManager>();
        if (buildingPlacer   == null) buildingPlacer   = FindAnyObjectByType<BuildingPlacer>();
        if (selectionManager == null) selectionManager = FindAnyObjectByType<SelectionManager>();

        EnsureEventSystem();

        Canvas canvas = BuildCanvas();
        BuildTopBar(canvas);
        BuildBottomBar(canvas);
        BuildInfoPanel(canvas);

        if (selectionManager != null)
        {
            selectionManager.OnBuildingSelected += _ => RefreshInfoPanel();
            selectionManager.OnAgentSelected    += _ => RefreshInfoPanel();
            selectionManager.OnSelectionCleared += HideInfoPanel;
        }
    }

    void Update()
    {
        // Keep time display fresh
        if (timeText != null && timeManager != null)
            timeText.text = timeManager.TimeString;

        // Sync build panel state when tile is deselected externally (ESC key)
        if (buildingPlacer != null && buildingPlacer.CurrentSelectedTile == null && activeBuildIdx >= 0)
        {
            SetActiveBuildButton(-1);
            if (buildPanel != null && buildPanel.activeSelf)
            {
                buildPanel.SetActive(false);
                ApplyActiveState(buildToggleButton, false);
            }
        }

        // Keep info panel live while something is selected
        if (infoPanel != null && infoPanel.activeSelf)
        {
            // Clear if the selected object was destroyed
            if (selectionManager != null &&
                selectionManager.SelectedBuilding == null &&
                selectionManager.SelectedAgent    == null)
            {
                HideInfoPanel();
            }
            else
            {
                RefreshInfoPanel();
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Canvas bootstrap
    // ─────────────────────────────────────────────────────────────────────────

    void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;

        GameObject esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGo.AddComponent<StandaloneInputModule>();
#endif
    }

    Canvas BuildCanvas()
    {
        GameObject go = new GameObject("GameUI Canvas");
        Canvas canvas  = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler       = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Top bar – time display (left) + speed buttons (right)
    // ─────────────────────────────────────────────────────────────────────────

    void BuildTopBar(Canvas canvas)
    {
        const float barH = 50f;

        GameObject bar = MakePanel(canvas.transform, PanelBg);
        SetAnchors(bar, 0f, 1f, 1f, 1f, pivot: new Vector2(0.5f, 1f));
        bar.GetComponent<RectTransform>().sizeDelta = new Vector2(0, barH);

        // Time text – left side
        GameObject timeGo = MakeText(bar.transform, "Day 1 – 06:00", 16, TextAnchor.MiddleLeft, White);
        RectTransform tr = timeGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(14f, 0f);
        tr.offsetMax = Vector2.zero;
        timeText = timeGo.GetComponent<Text>();

        // Speed button group – right side
        GameObject grp = new GameObject("SpeedGroup");
        grp.transform.SetParent(bar.transform, false);
        RectTransform gr = grp.AddComponent<RectTransform>();
        gr.anchorMin       = new Vector2(1f, 0.5f);
        gr.anchorMax       = new Vector2(1f, 0.5f);
        gr.pivot           = new Vector2(1f, 0.5f);
        gr.sizeDelta       = new Vector2(326f, 38f);
        gr.anchoredPosition = new Vector2(-8f, 0f);

        HorizontalLayoutGroup hlg = grp.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleRight;
        hlg.spacing              = 6f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;

        string[] labels = { "Pause", "1x", "5x", "10x" };
        float[]  speeds = { 0f, 1f, 5f, 10f };
        speedButtons = new Button[4];

        for (int i = 0; i < 4; i++)
        {
            float spd = speeds[i]; int idx = i;
            GameObject btn = MakeSimpleButton(grp.transform, labels[i], 13, () => OnSpeedClicked(spd, idx));
            btn.AddComponent<LayoutElement>().preferredWidth = 74f;
            speedButtons[i] = btn.GetComponent<Button>();
        }

        SetActiveSpeedButton(1); // 1x highlighted on start
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bottom bar – build toggle + build panel
    // ─────────────────────────────────────────────────────────────────────────

    void BuildBottomBar(Canvas canvas)
    {
        const float barH   = 60f;
        const float panelH = 90f;

        // Bottom strip
        GameObject bar = MakePanel(canvas.transform, PanelBg);
        SetAnchors(bar, 0f, 0f, 1f, 0f, pivot: new Vector2(0.5f, 0f));
        bar.GetComponent<RectTransform>().sizeDelta = new Vector2(0, barH);

        // Build toggle button
        GameObject buildBtn = MakeSimpleButton(bar.transform, "Build", 14, ToggleBuildPanel);
        RectTransform bbr = buildBtn.GetComponent<RectTransform>();
        bbr.anchorMin       = new Vector2(0f, 0.5f);
        bbr.anchorMax       = new Vector2(0f, 0.5f);
        bbr.pivot           = new Vector2(0f, 0.5f);
        bbr.sizeDelta       = new Vector2(90f, 40f);
        bbr.anchoredPosition = new Vector2(10f, 0f);
        buildToggleButton = buildBtn.GetComponent<Button>();

        // Build panel – sits above the bottom bar, hidden by default
        buildPanel = MakePanel(canvas.transform, PanelBg);
        SetAnchors(buildPanel, 0f, 0f, 1f, 0f, pivot: new Vector2(0.5f, 0f));
        RectTransform bpr = buildPanel.GetComponent<RectTransform>();
        bpr.sizeDelta       = new Vector2(0f, panelH);
        bpr.anchoredPosition = new Vector2(0f, barH);
        buildPanel.SetActive(false);

        // Button group inside build panel
        GameObject grp = new GameObject("BuildBtnGroup");
        grp.transform.SetParent(buildPanel.transform, false);
        RectTransform bgr = grp.AddComponent<RectTransform>();
        bgr.anchorMin = Vector2.zero;
        bgr.anchorMax = Vector2.one;
        bgr.offsetMin = new Vector2(8f,  6f);
        bgr.offsetMax = new Vector2(-8f, -6f);

        HorizontalLayoutGroup hlg = grp.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment       = TextAnchor.MiddleLeft;
        hlg.spacing              = 6f;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;

        // Building definitions – order matches buildButtons[]
        string[] lbls  = { "Road", "House", "Burger Store", "Supermarket", "Office", "Park", "Police", "Fire Stn" };
        Sprite[] icons = { roadIcon, houseIcon, burgerStoreIcon, supermarketIcon, officeIcon, parkIcon, policeStationIcon, fireStationIcon };
        TileBase[] tiles = buildingPlacer != null
            ? new TileBase[] {
                buildingPlacer.roadTile,
                buildingPlacer.houseTile,
                buildingPlacer.burgerStoreTile,
                buildingPlacer.supermarketTile,
                buildingPlacer.officeTile,
                buildingPlacer.parkTile,
                buildingPlacer.policeStationTile,
                buildingPlacer.fireStationTile }
            : new TileBase[8];

        buildButtons = new Button[lbls.Length];
        for (int i = 0; i < lbls.Length; i++)
        {
            int      idx  = i;
            TileBase tile = tiles[i];
            string   lbl  = lbls[i];

            GameObject btn = MakeIconButton(grp.transform, lbl, icons[i], () => OnBuildTileSelected(idx, tile, lbl));
            btn.AddComponent<LayoutElement>().preferredWidth = 92f;
            buildButtons[i] = btn.GetComponent<Button>();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event handlers
    // ─────────────────────────────────────────────────────────────────────────

    void OnSpeedClicked(float speed, int idx)
    {
        timeManager?.SetSpeed(speed);
        SetActiveSpeedButton(idx);
    }

    void ToggleBuildPanel()
    {
        bool opening = !buildPanel.activeSelf;
        buildPanel.SetActive(opening);
        ApplyActiveState(buildToggleButton, opening);

        if (!opening)
            ExitBuildMode();
    }

    void ExitBuildMode()
    {
        buildingPlacer?.SelectTile(null, "None");
        SetActiveBuildButton(-1);
        ApplyActiveState(buildToggleButton, false);
    }

    void OnBuildTileSelected(int idx, TileBase tile, string label)
    {
        buildingPlacer?.SelectTile(tile, label);
        SetActiveBuildButton(idx);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Button highlight helpers
    // ─────────────────────────────────────────────────────────────────────────

    void SetActiveSpeedButton(int activeIdx)
    {
        for (int i = 0; i < speedButtons.Length; i++)
            ApplyActiveState(speedButtons[i], i == activeIdx);
    }

    void SetActiveBuildButton(int activeIdx)
    {
        activeBuildIdx = activeIdx;
        for (int i = 0; i < buildButtons.Length; i++)
            ApplyActiveState(buildButtons[i], i == activeIdx);
    }

    void ApplyActiveState(Button btn, bool active)
    {
        if (btn == null) return;
        ColorBlock cb        = btn.colors;
        cb.normalColor       = active ? BtnActive : BtnNormal;
        cb.highlightedColor  = active ? BtnActive : BtnHover;
        cb.pressedColor      = BtnPressed;
        cb.selectedColor     = active ? BtnActive : BtnNormal;
        btn.colors = cb;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Info panel – selection details
    // ─────────────────────────────────────────────────────────────────────────

    void BuildInfoPanel(Canvas canvas)
    {
        // Panel: top-right, below the top bar, fixed size
        infoPanel = MakePanel(canvas.transform, PanelBg);
        RectTransform r = infoPanel.GetComponent<RectTransform>();
        r.anchorMin       = new Vector2(1f, 1f);
        r.anchorMax       = new Vector2(1f, 1f);
        r.pivot           = new Vector2(1f, 1f);
        r.sizeDelta       = new Vector2(240f, 320f);
        r.anchoredPosition = new Vector2(-8f, -58f); // 8px from right, below top bar

        // Title text (top strip, 44px tall)
        // Use offsetMin/offsetMax directly — mixing sizeDelta with offsetMin/Max collapses height to 0.
        GameObject titleGo = MakeText(infoPanel.transform, "", 15, TextAnchor.MiddleLeft, White);
        RectTransform tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot     = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(10f, -44f);  // left inset 10, extends 44px down from top anchor
        tr.offsetMax = new Vector2(-10f,  0f);  // right inset 10, flush with top anchor
        infoPanelTitle = titleGo.GetComponent<Text>();

        // Content text (fills rest of panel)
        GameObject contentGo = MakeText(infoPanel.transform, "", 11, TextAnchor.UpperLeft, new Color(0.88f, 0.88f, 0.88f));
        RectTransform cr = contentGo.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.offsetMin = new Vector2(10f, 8f);
        cr.offsetMax = new Vector2(-10f, -48f);
        infoPanelContent = contentGo.GetComponent<Text>();
        infoPanelContent.GetComponent<Text>().horizontalOverflow = HorizontalWrapMode.Wrap;

        infoPanel.SetActive(false);
    }

    void RefreshInfoPanel()
    {
        if (infoPanel == null || selectionManager == null) return;

        if (selectionManager.SelectedBuilding != null)
        {
            Building b = selectionManager.SelectedBuilding;
            infoPanelTitle.text   = b.buildingName;
            infoPanelContent.text = BuildingInfoText(b);
            infoPanel.SetActive(true);
        }
        else if (selectionManager.SelectedAgent != null)
        {
            Agent a = selectionManager.SelectedAgent;
            infoPanelTitle.text   = a.agentName;
            infoPanelContent.text = AgentInfoText(a);
            infoPanel.SetActive(true);
        }
    }

    void HideInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // ── Info text builders ────────────────────────────────────────────────────

    string BuildingInfoText(Building b)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("──────────────────────");

        if (b is ResidentialBuilding res)
        {
            sb.AppendLine("Type: Residential");
            sb.AppendLine($"Quality:  {NeedsBar(b.quality)} {b.quality:0}%");
            sb.AppendLine($"Treasury: ${b.treasury}");
            sb.AppendLine($"On Fire:  {(b.isOnFire ? "YES  !" : "No")}");
            sb.AppendLine();
            // Occupant list
            int totalOccupants = 0;
            foreach (var unit in res.DwellingUnits)
            {
                totalOccupants += unit.DwellingOccupancy.Count;
                foreach (Agent occupant in unit.DwellingOccupancy)
                    sb.AppendLine($"  {occupant.agentName}");
                sb.AppendLine($"  Pantry: {unit.pantry.Get(ItemType.Groceries)} groceries");
            }
            if (totalOccupants == 0) sb.AppendLine("  (vacant)");
        }
        else if (b is CommercialBuilding com)
        {
            sb.AppendLine($"Type: {b.buildingType}");
            sb.AppendLine($"Quality:  {NeedsBar(b.quality)} {b.quality:0}%");
            sb.AppendLine($"Treasury: ${b.treasury}");
            sb.AppendLine($"Status:   {(com.IsOpen() ? "OPEN" : "Closed")}");
            sb.AppendLine($"On Fire:  {(b.isOnFire ? "YES  !" : "No")}");
            sb.AppendLine();
            // Shift summary
            foreach (var shift in com.shifts)
            {
                int endHour = (shift.startHour + shift.durationHours) % 24;
                sb.AppendLine($"Shift {shift.startHour:00}:00–{endHour:00}:00");
                sb.AppendLine($"  Workers: {shift.AssignedWorkers.Count}/{shift.workersRequired}");
                foreach (Agent w in shift.AssignedWorkers)
                    sb.AppendLine($"    {w.agentName}");
            }
        }
        else
        {
            sb.AppendLine($"Type: {b.buildingType}");
            sb.AppendLine($"Quality:  {NeedsBar(b.quality)} {b.quality:0}%");
            sb.AppendLine($"Treasury: ${b.treasury}");
            sb.AppendLine($"On Fire:  {(b.isOnFire ? "YES  !" : "No")}");
        }

        sb.AppendLine();
        sb.Append($"Grid: ({b.gridPosition.x}, {b.gridPosition.y})");
        return sb.ToString();
    }

    string AgentInfoText(Agent a)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(a.agentName);
        sb.AppendLine("──────────────────────");
        sb.AppendLine($"State: {a.currentState}");
        sb.AppendLine();
        sb.AppendLine($"Hunger:  {NeedsBar(a.hunger)}  {a.hunger:0}%");
        sb.AppendLine($"Lonely:  {NeedsBar(a.loneliness)}  {a.loneliness:0}%");
        sb.AppendLine();
        sb.AppendLine($"Balance: ${a.bankBalance}");
        sb.AppendLine($"Job:     {(a.hasJob && a.employer != null ? a.employer.buildingName : "None")}");
        sb.AppendLine($"Home:    {(a.hasHome ? $"({a.homeTile.x},{a.homeTile.y})" : "None")}");
        sb.AppendLine();
        sb.Append($"Traits:  {a.personality}");
        return sb.ToString();
    }

    /// Returns an 8-char block bar for a 0–100 value.
    string NeedsBar(float val, float max = 100f, int width = 8)
    {
        int fill = Mathf.Clamp(Mathf.RoundToInt((val / max) * width), 0, width);
        return new string('\u2588', fill) + new string('\u2591', width - fill);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI factory helpers
    // ─────────────────────────────────────────────────────────────────────────

    // Panel (dark background rectangle).
    GameObject MakePanel(Transform parent, Color color)
    {
        GameObject go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    // Plain text label.
    GameObject MakeText(Transform parent, string content, int fontSize, TextAnchor align, Color color)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        Text t      = go.AddComponent<Text>();
        t.text      = content;
        t.fontSize  = fontSize;
        t.alignment = align;
        t.color     = color;
        t.font      = defaultFont;
        return go;
    }

    // Text-only button – caller sizes the RectTransform or uses a LayoutElement.
    GameObject MakeSimpleButton(Transform parent, string label, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "_btn");
        go.transform.SetParent(parent, false);

        Image img  = go.AddComponent<Image>();
        img.color  = BtnNormal;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = MakeColorBlock(BtnNormal, BtnHover, BtnPressed);
        btn.onClick.AddListener(onClick);

        // Child label
        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        Text t       = textGo.AddComponent<Text>();
        t.text       = label;
        t.fontSize   = fontSize;
        t.alignment  = TextAnchor.MiddleCenter;
        t.color      = White;
        t.font       = defaultFont;

        return go;
    }

    // Icon button – sprite on top, text label below (used in the build panel).
    GameObject MakeIconButton(Transform parent, string label, Sprite icon, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(label + "_btn");
        go.transform.SetParent(parent, false);

        Image img  = go.AddComponent<Image>();
        img.color  = BtnNormal;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = MakeColorBlock(BtnNormal, BtnHover, BtnPressed);
        btn.onClick.AddListener(onClick);

        bool hasIcon = icon != null;

        if (hasIcon)
        {
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            Image iImg = iconGo.AddComponent<Image>();
            iImg.sprite          = icon;
            iImg.preserveAspect  = true;
            RectTransform ir = iconGo.GetComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.10f, 0.36f);
            ir.anchorMax = new Vector2(0.90f, 0.92f);
            ir.offsetMin = ir.offsetMax = Vector2.zero;
        }

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        RectTransform tr = textGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, hasIcon ? 0.38f : 1f);
        tr.offsetMin = tr.offsetMax = Vector2.zero;
        Text t       = textGo.AddComponent<Text>();
        t.text       = label;
        t.fontSize   = hasIcon ? 10 : 13;
        t.alignment  = TextAnchor.MiddleCenter;
        t.color      = White;
        t.font       = defaultFont;

        return go;
    }

    // ── RectTransform helpers ─────────────────────────────────────────────────

    void SetAnchors(GameObject go, float minX, float minY, float maxX, float maxY, Vector2 pivot)
    {
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin       = new Vector2(minX, minY);
        r.anchorMax       = new Vector2(maxX, maxY);
        r.pivot           = pivot;
        r.anchoredPosition = Vector2.zero;
    }

    ColorBlock MakeColorBlock(Color normal, Color hover, Color pressed)
    {
        ColorBlock cb        = ColorBlock.defaultColorBlock;
        cb.normalColor       = normal;
        cb.highlightedColor  = hover;
        cb.pressedColor      = pressed;
        cb.selectedColor     = normal;
        cb.colorMultiplier   = 1f;
        return cb;
    }
}
