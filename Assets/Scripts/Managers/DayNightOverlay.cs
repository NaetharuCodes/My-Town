using UnityEngine;
using UnityEngine.UI;

// Applies a full-screen color overlay that follows the time of day.
//
// Setup:
//   1. Create a Canvas (Screen Space – Overlay, high Sort Order so it draws on top).
//   2. Add a child Image: anchor stretch-fill, no sprite, Raycast Target OFF.
//   3. Attach this component anywhere; drag in the Image and the TimeManager.
//   4. Edit DayNightGradient in the Inspector — time runs left (midnight) → right (midnight).
//      Suggested stops:
//        00:00  #0D1433  alpha ~170   (deep night)
//        05:00  #1A1A2E  alpha ~130   (pre-dawn)
//        06:30  #FF8C42  alpha ~60    (warm sunrise)
//        08:00  #000000  alpha 0      (full day — transparent)
//        18:00  #000000  alpha 0      (still day)
//        20:00  #FF6B35  alpha ~50    (sunset)
//        22:00  #0D1433  alpha ~170   (night)
//        24:00  #0D1433  alpha ~170   (wraps back to midnight)
public class DayNightOverlay : MonoBehaviour
{
    [Header("References")]
    public TimeManager timeManager;
    public Image       overlayImage;

    [Header("Color curve (x = 0 is midnight, x = 1 is next midnight)")]
    public Gradient dayNightGradient = DefaultGradient();

    [Header("Transition smoothing")]
    [Tooltip("How fast the overlay colour lerps toward the target (units/sec). "
           + "Higher = snappier hour transitions.")]
    public float lerpSpeed = 1.5f;

    private Color _targetColor;

    // ── Unity lifecycle ─────────────────────────────────────────────────────────
    private void Start()
    {
        if (timeManager == null)
            timeManager = FindFirstObjectByType<TimeManager>();

        if (timeManager != null)
        {
            timeManager.OnHourChanged += OnHourChanged;
            // Initialise without lerping so the overlay is correct immediately.
            _targetColor = overlayImage.color = SampleGradient(timeManager.CurrentHour);
        }
    }

    private void OnDestroy()
    {
        if (timeManager != null)
            timeManager.OnHourChanged -= OnHourChanged;
    }

    private void Update()
    {
        if (overlayImage == null) return;
        overlayImage.color = Color.Lerp(overlayImage.color, _targetColor,
                                        Time.deltaTime * lerpSpeed);
    }

    // ── Time callback ───────────────────────────────────────────────────────────
    private void OnHourChanged(int hour)
    {
        _targetColor = SampleGradient(hour);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────
    // Maps a 0-23 hour to a 0-1 gradient position.
    private Color SampleGradient(int hour) => dayNightGradient.Evaluate(hour / 24f);

    // Provides a sensible default so the overlay works out-of-the-box even before
    // the designer customises it in the Inspector.
    private static Gradient DefaultGradient()
    {
        var g = new Gradient();

        g.SetKeys(
            new GradientColorKey[]
            {
                new(new Color(0.05f, 0.08f, 0.20f), 0.00f),  // midnight — deep blue
                new(new Color(0.05f, 0.08f, 0.20f), 0.20f),  // 4:48 am  — still night
                new(new Color(1.00f, 0.55f, 0.26f), 0.27f),  // 6:29 am  — sunrise orange
                new(new Color(0.00f, 0.00f, 0.00f), 0.33f),  // 8:00 am  — full day (black @ 0 alpha)
                new(new Color(0.00f, 0.00f, 0.00f), 0.75f),  // 6:00 pm  — still day
                new(new Color(1.00f, 0.42f, 0.21f), 0.85f),  // 8:24 pm  — sunset orange
                new(new Color(0.05f, 0.08f, 0.20f), 0.92f),  // 10:00 pm — night returns
                new(new Color(0.05f, 0.08f, 0.20f), 1.00f),  // midnight — wraps
            },
            new GradientAlphaKey[]
            {
                new(0.67f, 0.00f),   // midnight   — dark
                new(0.67f, 0.20f),   // pre-dawn   — dark
                new(0.24f, 0.27f),   // sunrise    — faint warm tint
                new(0.00f, 0.33f),   // day        — transparent
                new(0.00f, 0.75f),   // late day   — transparent
                new(0.20f, 0.85f),   // sunset     — faint warm tint
                new(0.67f, 0.92f),   // night      — dark
                new(0.67f, 1.00f),   // midnight   — wraps
            }
        );

        return g;
    }
}
