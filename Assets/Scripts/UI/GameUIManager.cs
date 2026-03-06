using System.Collections;
using System.Collections.Generic;
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
    public Sprite arrivalPointIcon;
    public Sprite schoolIcon;
    public Sprite preschoolIcon;

    // ── live UI references ────────────────────────────────────────────────────
    private Canvas     mainCanvas;
    private Text       timeText;
    private GameObject buildPanel;
    private Button[]   speedButtons;
    private Button[]   buildButtons;
    private int        activeBuildIdx = -1;

    // Build mode
    private Button     buildToggleButton;

    // Inspection modal
    private GameObject inspectBackdrop;    // full-screen dimmer + click-to-close
    private GameObject inspectModal;       // the visible panel
    private Text       inspectTitle;       // modal title (building/agent name)
    private GameObject tabStrip;           // parent of tab buttons
    private List<Button> inspectTabs = new List<Button>();
    private Text       inspectContent;     // scrollable content text
    private string[]   inspectTabContent;  // pre-built content strings per tab
    private int        activeTabIdx;

    // Interior overlay
    private GameObject                      interiorOverlayRoot;
    private Coroutine                       interiorRefreshCoroutine;
    private Dictionary<string, GameObject>  interiorDots = new();   // agentName → dot GO

    // Interior zone panels (cached for dot parenting)
    private RectTransform interiorKitchenZone;
    private RectTransform interiorCounterZone;
    private RectTransform interiorQueueZone;
    private RectTransform interiorSeatingZone;

    // Slot positions (normalised 0-1 within each zone's rect)
    private static readonly Vector2[] KitchenSlots = {
        new(0.25f, 0.5f), new(0.5f, 0.5f), new(0.75f, 0.5f)
    };
    private static readonly Vector2[] CounterSlots = {
        new(0.3f, 0.5f), new(0.7f, 0.5f)
    };
    private static readonly Vector2[] QueueSlots = {
        new(0.10f, 0.5f), new(0.26f, 0.5f), new(0.42f, 0.5f),
        new(0.58f, 0.5f), new(0.74f, 0.5f), new(0.90f, 0.5f)
    };
    private static readonly Vector2[] SeatingSlots = {
        new(0.12f, 0.65f), new(0.25f, 0.65f),
        new(0.42f, 0.65f), new(0.55f, 0.65f),
        new(0.72f, 0.65f), new(0.85f, 0.65f),
        new(0.30f, 0.25f), new(0.65f, 0.25f),
    };

    // Dot colours
    private static readonly Color DotWorker  = new Color(0.29f, 0.62f, 1.00f); // blue
    private static readonly Color DotQueue   = new Color(1.00f, 0.55f, 0.26f); // orange
    private static readonly Color DotEating  = new Color(0.32f, 0.83f, 0.48f); // green

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

        mainCanvas = BuildCanvas();
        BuildTopBar(mainCanvas);
        BuildBottomBar(mainCanvas);
        BuildInspectModal(mainCanvas);

        if (selectionManager != null)
        {
            selectionManager.OnTileInspected    += ShowInspectModal;
            selectionManager.OnSelectionCleared += HideInspectModal;
        }
    }

    void Update()
    {
        // Keep time display fresh
        if (timeText != null && timeManager != null)
            timeText.text = timeManager.TimeString;

        // Sync build panel state when tile/delete-mode is cleared externally (ESC key)
        if (buildingPlacer != null && buildingPlacer.CurrentSelectedTile == null
            && !buildingPlacer.DeleteModeActive && activeBuildIdx >= 0)
        {
            SetActiveBuildButton(-1);
            if (buildPanel != null && buildPanel.activeSelf)
            {
                buildPanel.SetActive(false);
                ApplyActiveState(buildToggleButton, false);
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
        // Last entry is the Delete (demolish) tool — no tile, no icon.
        string[] lbls  = { "Road", "House", "Burger Store", "Supermarket", "Office", "Park", "Police", "Fire Stn", "Bus Stop", "School", "Preschool", "Delete" };
        Sprite[] icons = { roadIcon, houseIcon, burgerStoreIcon, supermarketIcon, officeIcon, parkIcon, policeStationIcon, fireStationIcon, arrivalPointIcon, schoolIcon, preschoolIcon, null };
        TileBase[] tiles = buildingPlacer != null
            ? new TileBase[] {
                buildingPlacer.roadTile,
                buildingPlacer.houseTile,
                buildingPlacer.burgerStoreTile,
                buildingPlacer.supermarketTile,
                buildingPlacer.officeTile,
                buildingPlacer.parkTile,
                buildingPlacer.policeStationTile,
                buildingPlacer.fireStationTile,
                buildingPlacer.arrivalPointTile,
                buildingPlacer.schoolTile,
                buildingPlacer.preschoolTile,
                null }
            : new TileBase[12];

        buildButtons = new Button[lbls.Length];
        int deleteIdx = lbls.Length - 1;
        for (int i = 0; i < lbls.Length; i++)
        {
            int      idx  = i;
            TileBase tile = tiles[i];
            string   lbl  = lbls[i];

            UnityEngine.Events.UnityAction onClick = (idx == deleteIdx)
                ? (UnityEngine.Events.UnityAction)(() => OnDeleteModeSelected(deleteIdx))
                : () => OnBuildTileSelected(idx, tile, lbl);

            GameObject btn = MakeIconButton(grp.transform, lbl, icons[i], onClick);
            btn.AddComponent<LayoutElement>().preferredWidth = 84f;
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
        buildingPlacer?.SetDeleteMode(false);
        SetActiveBuildButton(-1);
        ApplyActiveState(buildToggleButton, false);
    }

    void OnDeleteModeSelected(int idx)
    {
        // Toggle: clicking Delete again while already active turns it off.
        if (buildingPlacer != null && buildingPlacer.DeleteModeActive)
        {
            buildingPlacer.SetDeleteMode(false);
            SetActiveBuildButton(-1);
        }
        else
        {
            buildingPlacer?.SetDeleteMode(true);
            SetActiveBuildButton(idx);
        }
    }

    void OnBuildTileSelected(int idx, TileBase tile, string label)
    {
        buildingPlacer?.SetDeleteMode(false); // clear delete mode if it was active
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
    // Inspection modal
    // ─────────────────────────────────────────────────────────────────────────

    void BuildInspectModal(Canvas canvas)
    {
        const float modalW = 520f;
        const float modalH = 500f;
        const float titleH = 44f;
        const float tabH   = 36f;

        // ── Backdrop (full-screen dimmer + click-outside-to-close) ─────────────
        inspectBackdrop = new GameObject("InspectBackdrop");
        inspectBackdrop.transform.SetParent(canvas.transform, false);
        Image bdImg = inspectBackdrop.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.45f);
        RectTransform bdr = inspectBackdrop.GetComponent<RectTransform>();
        bdr.anchorMin = Vector2.zero;
        bdr.anchorMax = Vector2.one;
        bdr.offsetMin = bdr.offsetMax = Vector2.zero;

        // Make the backdrop itself a button so clicks outside the modal close it
        Button bdBtn = inspectBackdrop.AddComponent<Button>();
        bdBtn.targetGraphic = bdImg;
        bdBtn.colors = MakeColorBlock(new Color(0,0,0,0), new Color(0,0,0,0.1f), new Color(0,0,0,0.2f));
        bdBtn.onClick.AddListener(HideInspectModal);
        inspectBackdrop.SetActive(false); // hide immediately — shown only when modal opens

        // ── Modal panel ────────────────────────────────────────────────────────
        inspectModal = MakePanel(canvas.transform, PanelBg);
        RectTransform mr = inspectModal.GetComponent<RectTransform>();
        mr.anchorMin       = new Vector2(0.5f, 0.5f);
        mr.anchorMax       = new Vector2(0.5f, 0.5f);
        mr.pivot           = new Vector2(0.5f, 0.5f);
        mr.sizeDelta       = new Vector2(modalW, modalH);
        mr.anchoredPosition = Vector2.zero;
        inspectModal.SetActive(false); // hide immediately — shown only when inspecting

        // ── Title bar ──────────────────────────────────────────────────────────
        GameObject titleBar = MakePanel(inspectModal.transform, new Color(0.07f, 0.07f, 0.09f, 1f));
        RectTransform tbr = titleBar.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f);
        tbr.anchorMax = new Vector2(1f, 1f);
        tbr.pivot     = new Vector2(0.5f, 1f);
        tbr.offsetMin = new Vector2(0f, -titleH);
        tbr.offsetMax = Vector2.zero;

        // Title text
        GameObject titleGo = MakeText(titleBar.transform, "", 15, TextAnchor.MiddleLeft, White);
        RectTransform ttr = titleGo.GetComponent<RectTransform>();
        ttr.anchorMin = Vector2.zero;
        ttr.anchorMax = Vector2.one;
        ttr.offsetMin = new Vector2(12f, 0f);
        ttr.offsetMax = new Vector2(-50f, 0f);
        inspectTitle = titleGo.GetComponent<Text>();

        // Close [X] button
        GameObject closeBtn = MakeSimpleButton(titleBar.transform, "X", 14, HideInspectModal);
        RectTransform cr = closeBtn.GetComponent<RectTransform>();
        cr.anchorMin       = new Vector2(1f, 0.5f);
        cr.anchorMax       = new Vector2(1f, 0.5f);
        cr.pivot           = new Vector2(1f, 0.5f);
        cr.sizeDelta       = new Vector2(36f, 28f);
        cr.anchoredPosition = new Vector2(-8f, 0f);

        // ── Tab strip ──────────────────────────────────────────────────────────
        tabStrip = new GameObject("TabStrip");
        tabStrip.transform.SetParent(inspectModal.transform, false);
        RectTransform tsr = tabStrip.AddComponent<RectTransform>();
        tsr.anchorMin = new Vector2(0f, 1f);
        tsr.anchorMax = new Vector2(1f, 1f);
        tsr.pivot     = new Vector2(0.5f, 1f);
        tsr.offsetMin = new Vector2(0f, -(titleH + tabH));
        tsr.offsetMax = new Vector2(0f, -titleH);

        // Tabs are built dynamically in ShowInspectModal

        // ── Scrollable content area ────────────────────────────────────────────
        // Simple overflowing text – avoids ScrollRect layout-pass timing issues.
        // Content that exceeds the panel height will overflow downward (out of view)
        // but is always visible from the top, which is the right default for inspection.
        float contentTopOffset = titleH + tabH;

        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(inspectModal.transform, false);
        RectTransform conr = contentGo.AddComponent<RectTransform>();
        conr.anchorMin = Vector2.zero;
        conr.anchorMax = Vector2.one;
        conr.offsetMin = new Vector2(12f, 8f);
        conr.offsetMax = new Vector2(-12f, -contentTopOffset);

        Text contentText = contentGo.AddComponent<Text>();
        contentText.font                = defaultFont;
        contentText.fontSize            = 11;
        contentText.color               = new Color(0.88f, 0.88f, 0.88f);
        contentText.alignment           = TextAnchor.UpperLeft;
        contentText.horizontalOverflow  = HorizontalWrapMode.Wrap;
        contentText.verticalOverflow    = VerticalWrapMode.Overflow;
        inspectContent = contentText;
    }

    void ShowInspectModal(Building building)
    {
        if (building == null) return;

        // Interior-capable buildings get the schematic overlay instead.
        if (building is IHasInteriorView interiorBuilding)
        {
            OpenInteriorOverlay(interiorBuilding);
            return;
        }

        // ── Build tab labels + content strings ────────────────────────────────
        var labels   = new List<string> { building.buildingName };
        var contents = new List<string> { BuildingInfoText(building) };

        inspectTabContent = contents.ToArray();

        // ── Title ──────────────────────────────────────────────────────────────
        inspectTitle.text = building.buildingName;

        // ── Rebuild tab buttons ────────────────────────────────────────────────
        foreach (Button old in inspectTabs)
            if (old != null) Destroy(old.gameObject);
        inspectTabs.Clear();

        float modalW   = 520f;
        float tabW     = Mathf.Max(60f, modalW / labels.Count);
        int   tabFontSize = tabW < 80f ? 9 : 11;

        for (int i = 0; i < labels.Count; i++)
        {
            int capturedIdx = i;
            GameObject tabGo = MakeSimpleButton(tabStrip.transform, labels[i], tabFontSize,
                () => SelectInspectTab(capturedIdx));

            RectTransform tr = tabGo.GetComponent<RectTransform>();
            tr.anchorMin       = new Vector2(0f, 0f);
            tr.anchorMax       = new Vector2(0f, 1f);
            tr.pivot           = new Vector2(0f, 0.5f);
            tr.sizeDelta       = new Vector2(tabW, 0f);
            tr.anchoredPosition = new Vector2(i * tabW, 0f);

            inspectTabs.Add(tabGo.GetComponent<Button>());
        }

        // ── Show first tab ─────────────────────────────────────────────────────
        activeTabIdx = 0;
        SelectInspectTab(0);

        inspectBackdrop.SetActive(true);
        inspectModal.SetActive(true);
    }

    void SelectInspectTab(int idx)
    {
        if (inspectTabContent == null || idx >= inspectTabContent.Length) return;
        activeTabIdx = idx;
        inspectContent.text = inspectTabContent[idx];

        for (int i = 0; i < inspectTabs.Count; i++)
            ApplyActiveState(inspectTabs[i], i == idx);
    }

    void HideInspectModal()
    {
        if (inspectBackdrop != null) inspectBackdrop.SetActive(false);
        if (inspectModal    != null) inspectModal.SetActive(false);
        CloseInteriorOverlay();
    }

    // ── Info text builders ────────────────────────────────────────────────────

    string BuildingInfoText(Building b)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("──────────────────────");

        if (b is ResidentialBuilding res)
        {
            // Tally totals
            int totalOccupants = 0;
            int totalBedrooms  = 0;
            foreach (var unit in res.DwellingUnits)
            {
                totalOccupants += unit.DwellingOccupancyV2.Count;
                totalBedrooms  += unit.NumberOfBedrooms;
            }

            // Headline row – bedrooms + occupancy at a glance
            string occStr = totalOccupants == 0
                ? "VACANT"
                : $"{totalOccupants} resident{(totalOccupants == 1 ? "" : "s")}";
            sb.AppendLine($"{totalBedrooms} bed  ·  {occStr}");
            sb.AppendLine($"Quality: {NeedsBar(b.quality)} {b.quality:0}%");
            sb.AppendLine($"Treasury: ${b.treasury}");
            if (b.isOnFire) sb.AppendLine("*** ON FIRE ***");
            sb.AppendLine();

            // Per-unit detail
            foreach (var unit in res.DwellingUnits)
            {
                if (res.DwellingUnits.Count > 1)
                    sb.AppendLine($"── Unit ({unit.NumberOfBedrooms} bed) ──────────");

                if (unit.DwellingOccupancyV2.Count == 0)
                {
                    sb.AppendLine("  (vacant)");
                }
                else
                {
                    foreach (AgentV2 occupant in unit.DwellingOccupancyV2)
                    {
                        string firstName = occupant.Name.Contains(' ')
                            ? occupant.Name.Split(' ')[0]
                            : occupant.Name;
                        sb.AppendLine($"  {firstName}");
                    }
                }

                sb.AppendLine($"  Pantry: {unit.pantry.Get(ItemType.Groceries)} groceries");
                sb.AppendLine($"  Rent:   ${unit.rentPerDay}/day");
                sb.AppendLine();
            }
        }
        else if (b is SchoolBuilding school)
        {
            string sessionLabel = (timeManager != null && school.IsInSession(timeManager.CurrentHour))
                ? "IN SESSION" : "Closed";
            sb.AppendLine($"Hours:     {school.openHour:00}:00 – {school.closeHour:00}:00");
            sb.AppendLine($"Status:    {sessionLabel}");
            sb.AppendLine($"Enrolled:  {school.EnrolledCount} / {school.maxStudents}");
            sb.AppendLine($"Present:   {school.PresentCount}");
            sb.AppendLine($"Treasury:  ${b.treasury}");
            sb.AppendLine($"On Fire:   {(b.isOnFire ? "YES  !" : "No")}");
            sb.AppendLine();
            // Teacher shift
            foreach (var shift in school.shifts)
            {
                int endHour = (shift.startHour + shift.durationHours) % 24;
                sb.AppendLine($"Teachers {shift.startHour:00}:00–{endHour:00}:00");
                sb.AppendLine($"  Filled: {shift.AssignedWorkersV2.Count}/{shift.workersRequired}");
                foreach (AgentV2 w in shift.AssignedWorkersV2)
                    sb.AppendLine($"    {w.Name}");
            }
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
                sb.AppendLine($"  Workers: {shift.AssignedWorkersV2.Count}/{shift.workersRequired}");
                foreach (AgentV2 w in shift.AssignedWorkersV2)
                    sb.AppendLine($"    {w.Name}");
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

    static string FormatLifeStage(LifeStage stage) => stage switch
    {
        LifeStage.Baby           => "Baby",
        LifeStage.Toddler        => "Toddler",
        LifeStage.YoungChild     => "Young Child",
        LifeStage.OlderChild     => "Older Child",
        LifeStage.Teen           => "Teen",
        LifeStage.Adult          => "Adult",
        LifeStage.Elder          => "Elder",
        LifeStage.VenerableElder => "Venerable Elder",
        _                        => stage.ToString()
    };

    static string FormatFamilyType(FamilyType type) => type switch
    {
        FamilyType.Solo          => "Solo",
        FamilyType.YoungCouple   => "Young Couple",
        FamilyType.SmallFamily   => "Small Family",
        FamilyType.LargeFamily   => "Large Family",
        FamilyType.RetiredCouple => "Retired Couple",
        FamilyType.SingleParent  => "Single Parent",
        _                        => type.ToString()
    };

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

    // ─────────────────────────────────────────────────────────────────────────
    // Interior overlay
    // ─────────────────────────────────────────────────────────────────────────

    void OpenInteriorOverlay(IHasInteriorView building)
    {
        CloseInteriorOverlay();  // close any previously open overlay

        if (mainCanvas == null) return;

        BuildInteriorPanel(mainCanvas, building);
        interiorRefreshCoroutine = StartCoroutine(InteriorRefreshLoop(building));
    }

    void CloseInteriorOverlay()
    {
        if (interiorRefreshCoroutine != null)
        {
            StopCoroutine(interiorRefreshCoroutine);
            interiorRefreshCoroutine = null;
        }

        if (interiorOverlayRoot != null)
        {
            Destroy(interiorOverlayRoot);
            interiorOverlayRoot  = null;
            interiorKitchenZone  = null;
            interiorCounterZone  = null;
            interiorQueueZone    = null;
            interiorSeatingZone  = null;
        }

        interiorDots.Clear();
    }

    void BuildInteriorPanel(Canvas canvas, IHasInteriorView building)
    {
        const float modalW   = 720f;
        const float modalH   = 520f;
        const float titleH   = 44f;
        const float statusH  = 30f;
        const float padding  = 10f;

        // ── Root (full-screen backdrop) ────────────────────────────────────────
        interiorOverlayRoot = new GameObject("InteriorOverlay");
        interiorOverlayRoot.transform.SetParent(canvas.transform, false);

        Image bdImg = interiorOverlayRoot.AddComponent<Image>();
        bdImg.color = new Color(0f, 0f, 0f, 0.6f);
        RectTransform bdr = interiorOverlayRoot.GetComponent<RectTransform>();
        bdr.anchorMin = Vector2.zero;
        bdr.anchorMax = Vector2.one;
        bdr.offsetMin = bdr.offsetMax = Vector2.zero;

        // Clicking the backdrop closes the overlay.
        Button bdBtn = interiorOverlayRoot.AddComponent<Button>();
        bdBtn.targetGraphic = bdImg;
        bdBtn.colors = MakeColorBlock(new Color(0,0,0,0), new Color(0,0,0,0.08f), new Color(0,0,0,0.15f));
        bdBtn.onClick.AddListener(CloseInteriorOverlay);

        // ── Modal panel ────────────────────────────────────────────────────────
        GameObject modal = MakePanel(interiorOverlayRoot.transform, PanelBg);
        RectTransform mr = modal.GetComponent<RectTransform>();
        mr.anchorMin       = new Vector2(0.5f, 0.5f);
        mr.anchorMax       = new Vector2(0.5f, 0.5f);
        mr.pivot           = new Vector2(0.5f, 0.5f);
        mr.sizeDelta       = new Vector2(modalW, modalH);
        mr.anchoredPosition = Vector2.zero;

        // Stop backdrop clicks from passing through the modal.
        modal.GetComponent<Image>().raycastTarget = true;

        // ── Title bar ──────────────────────────────────────────────────────────
        GameObject titleBar = MakePanel(modal.transform, new Color(0.07f, 0.07f, 0.09f, 1f));
        RectTransform tbr = titleBar.GetComponent<RectTransform>();
        tbr.anchorMin = new Vector2(0f, 1f);
        tbr.anchorMax = new Vector2(1f, 1f);
        tbr.pivot     = new Vector2(0.5f, 1f);
        tbr.offsetMin = new Vector2(0f, -titleH);
        tbr.offsetMax = Vector2.zero;

        GameObject titleGo = MakeText(titleBar.transform, building.InteriorDisplayName,
            15, TextAnchor.MiddleLeft, White);
        RectTransform ttr = titleGo.GetComponent<RectTransform>();
        ttr.anchorMin = Vector2.zero;
        ttr.anchorMax = Vector2.one;
        ttr.offsetMin = new Vector2(14f, 0f);
        ttr.offsetMax = new Vector2(-50f, 0f);

        GameObject closeBtn = MakeSimpleButton(titleBar.transform, "X", 14, CloseInteriorOverlay);
        RectTransform cr = closeBtn.GetComponent<RectTransform>();
        cr.anchorMin       = new Vector2(1f, 0.5f);
        cr.anchorMax       = new Vector2(1f, 0.5f);
        cr.pivot           = new Vector2(1f, 0.5f);
        cr.sizeDelta       = new Vector2(36f, 28f);
        cr.anchoredPosition = new Vector2(-8f, 0f);

        // ── Interior content area ──────────────────────────────────────────────
        float contentAreaH = modalH - titleH - statusH - padding * 2f;

        GameObject contentArea = new GameObject("InteriorContent");
        contentArea.transform.SetParent(modal.transform, false);
        RectTransform car = contentArea.AddComponent<RectTransform>();
        car.anchorMin = new Vector2(0f, 0f);
        car.anchorMax = new Vector2(1f, 1f);
        car.offsetMin = new Vector2(padding, statusH + padding);
        car.offsetMax = new Vector2(-padding, -titleH);

        // ── Zone panels ────────────────────────────────────────────────────────
        // Kitchen  – left 33%, top 58%
        interiorKitchenZone  = CreateZone(contentArea.transform, "KITCHEN",
            0f, 0.42f, 0.33f, 1f, new Color(0.17f, 0.10f, 0.06f));

        // Counter  – mid 33%, top 30%
        interiorCounterZone  = CreateZone(contentArea.transform, "COUNTER",
            0.33f, 0.70f, 0.66f, 1f, new Color(0.24f, 0.16f, 0.10f));

        // Queue    – right 67%, mid strip
        interiorQueueZone    = CreateZone(contentArea.transform, "QUEUE  →",
            0.33f, 0.42f, 1f, 0.70f, new Color(0.96f, 0.91f, 0.78f),
            labelColor: new Color(0.2f, 0.2f, 0.2f));

        // Seating  – full width, bottom 42%
        interiorSeatingZone  = CreateZone(contentArea.transform, "SEATING",
            0f, 0f, 1f, 0.42f, new Color(0.99f, 0.95f, 0.88f),
            labelColor: new Color(0.2f, 0.2f, 0.2f));

        // ── Status bar ─────────────────────────────────────────────────────────
        GameObject statusBar = MakePanel(modal.transform, new Color(0.07f, 0.07f, 0.09f, 1f));
        RectTransform sbr = statusBar.GetComponent<RectTransform>();
        sbr.anchorMin = new Vector2(0f, 0f);
        sbr.anchorMax = new Vector2(1f, 0f);
        sbr.pivot     = new Vector2(0.5f, 0f);
        sbr.offsetMin = Vector2.zero;
        sbr.offsetMax = new Vector2(0f, statusH);

        MakeText(statusBar.transform, "", 11, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f));

        // ── Legend ─────────────────────────────────────────────────────────────
        BuildInteriorLegend(statusBar.transform);
    }

    RectTransform CreateZone(Transform parent, string label,
        float ancMinX, float ancMinY, float ancMaxX, float ancMaxY,
        Color bgColor, Color? labelColor = null)
    {
        GameObject zone = new GameObject(label + "_zone");
        zone.transform.SetParent(parent, false);

        Image img   = zone.AddComponent<Image>();
        img.color   = bgColor;

        RectTransform r = zone.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(ancMinX, ancMinY);
        r.anchorMax = new Vector2(ancMaxX, ancMaxY);
        r.offsetMin = new Vector2(2f,  2f);
        r.offsetMax = new Vector2(-2f, -2f);

        // Zone label
        GameObject lgo = MakeText(zone.transform, label, 9, TextAnchor.UpperLeft,
            labelColor ?? new Color(0.8f, 0.7f, 0.6f));
        RectTransform lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(4f, 4f);
        lr.offsetMax = new Vector2(-4f, -4f);

        return r;
    }

    void BuildInteriorLegend(Transform parent)
    {
        // Tiny colour key: ■ Worker  ■ Queuing  ■ Eating
        (Color col, string lbl)[] entries = {
            (DotWorker, "Worker"),
            (DotQueue,  "Queuing"),
            (DotEating, "Eating"),
        };

        float startX = 14f;
        float stepX  = 110f;

        for (int i = 0; i < entries.Length; i++)
        {
            float x = startX + i * stepX;

            // Colour swatch
            GameObject swatch = new GameObject("Swatch");
            swatch.transform.SetParent(parent, false);
            swatch.AddComponent<Image>().color = entries[i].col;
            RectTransform sr = swatch.GetComponent<RectTransform>();
            sr.anchorMin       = new Vector2(0f, 0.5f);
            sr.anchorMax       = new Vector2(0f, 0.5f);
            sr.pivot           = new Vector2(0f, 0.5f);
            sr.sizeDelta       = new Vector2(12f, 12f);
            sr.anchoredPosition = new Vector2(x, 0f);

            // Label
            GameObject lgo = MakeText(parent, entries[i].lbl, 10, TextAnchor.MiddleLeft,
                new Color(0.75f, 0.75f, 0.75f));
            RectTransform lr = lgo.GetComponent<RectTransform>();
            lr.anchorMin       = new Vector2(0f, 0f);
            lr.anchorMax       = new Vector2(0f, 1f);
            lr.pivot           = new Vector2(0f, 0.5f);
            lr.sizeDelta       = new Vector2(80f, 0f);
            lr.anchoredPosition = new Vector2(x + 16f, 0f);
        }
    }

    IEnumerator InteriorRefreshLoop(IHasInteriorView building)
    {
        while (interiorOverlayRoot != null)
        {
            UpdateInteriorAgents(building);
            yield return new WaitForSeconds(0.5f);
        }
    }

    void UpdateInteriorAgents(IHasInteriorView building)
    {
        if (interiorOverlayRoot == null) return;

        List<InteriorAgentInfo> agents = building.GetInteriorAgentInfo();

        // Build a set of current agent names for diff.
        var currentNames = new HashSet<string>();
        foreach (var info in agents)
            currentNames.Add(info.name);

        // Remove dots for agents who have left.
        var toRemove = new List<string>();
        foreach (var kv in interiorDots)
            if (!currentNames.Contains(kv.Key)) toRemove.Add(kv.Key);
        foreach (var key in toRemove)
        {
            if (interiorDots[key] != null) Destroy(interiorDots[key]);
            interiorDots.Remove(key);
        }

        // Tally slots used per zone so we can assign positions.
        var slotIndex = new Dictionary<InteriorZone, int>
        {
            { InteriorZone.Kitchen, 0 },
            { InteriorZone.Counter, 0 },
            { InteriorZone.Queue,   0 },
            { InteriorZone.Seating, 0 },
        };

        foreach (var info in agents)
        {
            RectTransform zoneRT = ZoneRect(info.zone);
            if (zoneRT == null) continue;

            int idx = slotIndex[info.zone];
            Vector2[] slots = SlotsForZone(info.zone);
            if (idx >= slots.Length) continue;
            slotIndex[info.zone]++;

            Color dotColor = info.isWorker ? DotWorker
                           : info.zone == InteriorZone.Seating ? DotEating
                           : DotQueue;

            if (!interiorDots.TryGetValue(info.name, out GameObject dot) || dot == null)
            {
                dot = CreateAgentDot(zoneRT, info.name, dotColor);
                interiorDots[info.name] = dot;
            }

            // Reparent to correct zone if changed.
            if (dot.transform.parent != zoneRT)
                dot.transform.SetParent(zoneRT, false);

            // Update colour (Image lives on the "Dot" child, not the root).
            Image dotImg = dot.GetComponentInChildren<Image>();
            if (dotImg != null) dotImg.color = dotColor;

            // Position within zone using normalised slot coordinates.
            RectTransform dr = dot.GetComponent<RectTransform>();
            Rect zoneRect = zoneRT.rect;
            dr.anchoredPosition = new Vector2(
                (slots[idx].x - 0.5f) * zoneRect.width,
                (slots[idx].y - 0.5f) * zoneRect.height
            );
        }
    }

    GameObject CreateAgentDot(RectTransform zone, string agentName, Color color)
    {
        const float dotSize  = 26f;
        const float labelH   = 14f;

        GameObject root = new GameObject(agentName + "_dot");
        root.AddComponent<RectTransform>();   // must be added before parenting into Canvas
        root.transform.SetParent(zone, false);

        // Coloured square
        GameObject square = new GameObject("Dot");
        square.transform.SetParent(root.transform, false);
        Image img = square.AddComponent<Image>();
        img.color = color;
        RectTransform ir = square.GetComponent<RectTransform>();
        ir.anchorMin       = new Vector2(0.5f, 0.5f);
        ir.anchorMax       = new Vector2(0.5f, 0.5f);
        ir.pivot           = new Vector2(0.5f, 0.5f);
        ir.sizeDelta       = new Vector2(dotSize, dotSize);
        ir.anchoredPosition = new Vector2(0f, labelH * 0.5f);

        // Name label — white works on dark zones; on light zones the dot colour above gives enough context.
        string firstName = agentName.Contains(' ') ? agentName.Split(' ')[0] : agentName;
        GameObject lgo = MakeText(root.transform, firstName, 9, TextAnchor.MiddleCenter, White);
        RectTransform lr = lgo.GetComponent<RectTransform>();
        lr.anchorMin       = new Vector2(0.5f, 0.5f);
        lr.anchorMax       = new Vector2(0.5f, 0.5f);
        lr.pivot           = new Vector2(0.5f, 0.5f);
        lr.sizeDelta       = new Vector2(70f, labelH);
        lr.anchoredPosition = new Vector2(0f, -(dotSize * 0.5f + 1f));

        return root;
    }

    RectTransform ZoneRect(InteriorZone zone) => zone switch
    {
        InteriorZone.Kitchen => interiorKitchenZone,
        InteriorZone.Counter => interiorCounterZone,
        InteriorZone.Queue   => interiorQueueZone,
        InteriorZone.Seating => interiorSeatingZone,
        _                    => null,
    };

    static Vector2[] SlotsForZone(InteriorZone zone) => zone switch
    {
        InteriorZone.Kitchen => KitchenSlots,
        InteriorZone.Counter => CounterSlots,
        InteriorZone.Queue   => QueueSlots,
        InteriorZone.Seating => SeatingSlots,
        _                    => System.Array.Empty<Vector2>(),
    };

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
