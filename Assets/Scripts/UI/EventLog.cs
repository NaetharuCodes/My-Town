using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton event log. Displays a scrollable panel of timestamped game events.
///
/// Call from anywhere:
///   EventLog.Log("message");                         // grey – general info
///   EventLog.Log("message", LogCategory.Warning);   // orange
///   EventLog.LogDanger("building on fire!");         // red
///   EventLog.LogMoney("can't pay wages");            // gold
/// </summary>
public class EventLog : MonoBehaviour
{
    public static EventLog Instance { get; private set; }

    public enum LogCategory { Info, Warning, Danger, Money }

    // ── Config ────────────────────────────────────────────────────────────────
    private const int   MaxEntries = 80;
    private const float PanelW     = 340f;
    private const float PanelH     = 200f;
    private const float TitleH     = 26f;

    // ── Rich-text colours ─────────────────────────────────────────────────────
    private const string ColInfo    = "#BBBBBB";
    private const string ColWarning = "#FFA040";
    private const string ColDanger  = "#FF5555";
    private const string ColMoney   = "#FFD060";
    private const string ColTime    = "#555566";

    // ── UI palette ────────────────────────────────────────────────────────────
    private static readonly Color PanelBg  = new Color(0.10f, 0.10f, 0.12f, 0.92f);
    private static readonly Color TitleBg  = new Color(0.06f, 0.06f, 0.08f, 1.00f);
    private static readonly Color DimWhite = new Color(0.65f, 0.65f, 0.70f, 1.00f);

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly Queue<string> entries = new Queue<string>();
    private TimeManager  timeManager;
    private Text         logText;
    private ScrollRect   scrollRect;
    private Font         defaultFont;
    private bool         uiReady = false;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance    = this;
        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (defaultFont == null)
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void Start()
    {
        timeManager = FindAnyObjectByType<TimeManager>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public static void Log(string msg, LogCategory cat = LogCategory.Info)
        => Instance?.AddEntry(msg, cat);

    public static void LogWarning(string msg) => Log(msg, LogCategory.Warning);
    public static void LogDanger(string msg)  => Log(msg, LogCategory.Danger);
    public static void LogMoney(string msg)   => Log(msg, LogCategory.Money);

    // ─────────────────────────────────────────────────────────────────────────
    // Internal
    // ─────────────────────────────────────────────────────────────────────────

    void AddEntry(string msg, LogCategory cat)
    {
        if (!uiReady) TryBuildUI();
        if (!uiReady) return; // canvas not available yet

        string col = cat switch
        {
            LogCategory.Warning => ColWarning,
            LogCategory.Danger  => ColDanger,
            LogCategory.Money   => ColMoney,
            _                   => ColInfo,
        };

        string timePrefix = timeManager != null
            ? $"<color={ColTime}>[D{timeManager.CurrentDay} {timeManager.CurrentHour:00}:00]</color> "
            : "";

        entries.Enqueue($"{timePrefix}<color={col}>{msg}</color>");
        while (entries.Count > MaxEntries) entries.Dequeue();

        RebuildText();
        StartCoroutine(ScrollToBottom());
    }

    void RebuildText()
    {
        var sb = new StringBuilder();
        foreach (string line in entries)
            sb.AppendLine(line);
        if (logText != null)
            logText.text = sb.ToString();
    }

    IEnumerator ScrollToBottom()
    {
        // Wait one frame so ContentSizeFitter has a chance to resize the content
        yield return new WaitForEndOfFrame();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UI construction (lazy – deferred until a Canvas exists)
    // ─────────────────────────────────────────────────────────────────────────

    void TryBuildUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        // ── Outer panel ───────────────────────────────────────────────────────
        // Anchored to the bottom-left, sitting above the bottom bar + build panel
        // (bottom bar = 60px, build panel = 90px → place at y = 158)
        GameObject panel = new GameObject("EventLogPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.AddComponent<Image>().color = PanelBg;
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin        = new Vector2(0f, 0f);
        pr.anchorMax        = new Vector2(0f, 0f);
        pr.pivot            = new Vector2(0f, 0f);
        pr.sizeDelta        = new Vector2(PanelW, PanelH);
        pr.anchoredPosition = new Vector2(8f, 158f);

        // ── Title strip ───────────────────────────────────────────────────────
        GameObject titleGo = new GameObject("Title");
        titleGo.transform.SetParent(panel.transform, false);
        titleGo.AddComponent<Image>().color = TitleBg;
        RectTransform tr = titleGo.GetComponent<RectTransform>();
        tr.anchorMin        = new Vector2(0f, 1f);
        tr.anchorMax        = new Vector2(1f, 1f);
        tr.pivot            = new Vector2(0.5f, 1f);
        tr.sizeDelta        = new Vector2(0f, TitleH);
        tr.anchoredPosition = Vector2.zero;

        GameObject titleLabel = new GameObject("TitleLabel");
        titleLabel.transform.SetParent(titleGo.transform, false);
        Text tl = titleLabel.AddComponent<Text>();
        tl.text      = "Events";
        tl.fontSize  = 11;
        tl.font      = defaultFont;
        tl.color     = DimWhite;
        tl.alignment = TextAnchor.MiddleLeft;
        RectTransform tlr = titleLabel.GetComponent<RectTransform>();
        tlr.anchorMin = Vector2.zero;
        tlr.anchorMax = Vector2.one;
        tlr.offsetMin = new Vector2(8f, 0f);
        tlr.offsetMax = Vector2.zero;

        // ── ScrollRect ────────────────────────────────────────────────────────
        GameObject srGo = new GameObject("ScrollRect");
        srGo.transform.SetParent(panel.transform, false);
        scrollRect = srGo.AddComponent<ScrollRect>();
        scrollRect.horizontal        = false;
        scrollRect.vertical          = true;
        scrollRect.movementType      = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        RectTransform srr = srGo.GetComponent<RectTransform>();
        srr.anchorMin = Vector2.zero;
        srr.anchorMax = Vector2.one;
        srr.offsetMin = new Vector2(0f, 0f);
        srr.offsetMax = new Vector2(0f, -TitleH);

        // ── Viewport (with Mask) ──────────────────────────────────────────────
        GameObject vpGo = new GameObject("Viewport");
        vpGo.transform.SetParent(srGo.transform, false);
        Image vpImg = vpGo.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.01f); // near-transparent; required by Mask
        vpGo.AddComponent<Mask>().showMaskGraphic = false;
        RectTransform vpr = vpGo.GetComponent<RectTransform>();
        vpr.anchorMin = Vector2.zero;
        vpr.anchorMax = Vector2.one;
        vpr.offsetMin = vpr.offsetMax = Vector2.zero;
        scrollRect.viewport = vpr;

        // ── Content (auto-sizes via ContentSizeFitter) ────────────────────────
        // Anchored to the TOP so it grows downward; scroll to bottom shows latest.
        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(vpGo.transform, false);
        logText = contentGo.AddComponent<Text>();
        logText.text             = "";
        logText.fontSize         = 11;
        logText.font             = defaultFont;
        logText.color            = Color.white;
        logText.supportRichText  = true;
        logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        logText.verticalOverflow   = VerticalWrapMode.Overflow;
        logText.alignment          = TextAnchor.UpperLeft;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform cr = contentGo.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 1f); // top, full width
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot     = new Vector2(0f, 1f); // grows downward
        cr.offsetMin = new Vector2(6f, 0f);
        cr.offsetMax = new Vector2(-6f, 0f);
        cr.sizeDelta = new Vector2(cr.sizeDelta.x, 0f);
        scrollRect.content = cr;

        uiReady = true;
    }
}
