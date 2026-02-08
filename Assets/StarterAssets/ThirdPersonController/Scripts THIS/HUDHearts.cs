// HUDHearts.cs
// -> UI-Controller für 3 Herzen (grau -> pink)
// -> Wird NUR in besessenen Perspektiven angezeigt (über DialogueManager gesteuert)

using UnityEngine;
using UnityEngine.UI;

public class HUDHearts : MonoBehaviour
{
    public static HUDHearts Instance;

    [Header("Hearts (3 Images, order left->right)")]
    public Image[] hearts;                 // Size = 3
    public Sprite heartGray;
    public Sprite heartPink;

    [Header("Debug")]
    public bool debugLogs = false;

    int _filled = 0; // 0..3

    void Awake()
    {
        Instance = this;
        Apply();
    }

    // ============================================================
    // Public API
    // ============================================================

    /// <summary>
    /// Besessen-Perspektive: sichtbar machen + auf 3 graue Herzen resetten.
    /// </summary>
    public void ResetHeartsAndShow()
    {
        gameObject.SetActive(true);
        _filled = 0;
        Apply();

        if (debugLogs) Debug.Log("[HUDHearts] ResetHeartsAndShow()");
    }

    /// <summary>
    /// Default-Perspektive: komplett ausblenden.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
        if (debugLogs) Debug.Log("[HUDHearts] Hide()");
    }

    /// <summary>
    /// Füllt ein weiteres Herz (max 3).
    /// </summary>
    public void Advance()
    {
        _filled = Mathf.Clamp(_filled + 1, 0, 3);
        Apply();

        if (debugLogs) Debug.Log($"[HUDHearts] Advance() -> filled={_filled}");
    }

    /// <summary>
    /// Setzt exakt wie viele Herzen pink sind (0..3).
    /// </summary>
    public void SetFilled(int filled)
    {
        _filled = Mathf.Clamp(filled, 0, 3);
        Apply();

        if (debugLogs) Debug.Log($"[HUDHearts] SetFilled({filled}) -> filled={_filled}");
    }

    public int GetFilled() => _filled;

    // ============================================================
    // Intern
    // ============================================================
    void Apply()
    {
        if (hearts == null || hearts.Length == 0) return;

        for (int i = 0; i < hearts.Length; i++)
        {
            var img = hearts[i];
            if (!img) continue;

            bool pink = i < _filled;
            if (pink && heartPink) img.sprite = heartPink;
            else if (!pink && heartGray) img.sprite = heartGray;
        }
    }
}