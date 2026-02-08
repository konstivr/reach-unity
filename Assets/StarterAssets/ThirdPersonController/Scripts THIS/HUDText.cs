using UnityEngine;
using System.Collections;
using TMPro;

public class HUDText : MonoBehaviour
{
    public static HUDText Instance;

    [Header("One Text Element (TMP)")]
    public TMP_Text textTMP;

    [Header("Refs")]
    public PerspectiveSwapManager swapManager;

    [Header("Intro Sequence (3 texts)")]
    [Tooltip("Wenn true: Zeigt zu Beginn nacheinander Intro-Texte und wird danach leer.")]
    public bool playIntroOnStart = true;

    [Tooltip("Jeder Eintrag wird introSeconds lang angezeigt.")]
    [TextArea(1, 3)]
    public string[] introTexts = new string[]
    {
        "Reach out and press the left Button to interact",
        "Move closer to a person to reach out",
        "Hold the right button to speak"
    };

    [Min(0.1f)]
    public float introSeconds = 5f;

    [Header("Idle Texts")]
    [TextArea(1, 3)]
    public string initialIdleText = "Reach out and press the left Button to interact";

    [TextArea(1, 3)]
    public string perspectiveIdleText = "Talk to me";

    [Header("Debug")]
    public bool debugLogs = false;

    public enum Mode
    {
        IdleAuto,
        Prompt,
        StickyUntilReset,
        FXOverride,
        Intro
    }

    Mode _mode = Mode.IdleAuto;

    // timed prompts
    Coroutine _timedRoutine;

    // intro
    Coroutine _introRoutine;
    bool _introRunning = false;
    bool _introFinished = false;

    void Awake()
    {
        Instance = this;
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
    }

    void Start()
    {
        if (playIntroOnStart)
            StartIntroSequence();
        else
            ClearText();
    }

    public Mode CurrentMode => _mode;
    public bool IsLockedByFX => _mode == Mode.FXOverride;
    public bool IsSticky => _mode == Mode.StickyUntilReset;
    public bool IsIntroRunning => _introRunning;

    // =========================================================
    // Public API
    // =========================================================

    public void StartIntroSequence()
    {
        StopIntro();
        StopTimed();

        _introRoutine = StartCoroutine(CoIntro());
    }

    public void StopIntro()
    {
        if (_introRoutine != null)
        {
            StopCoroutine(_introRoutine);
            _introRoutine = null;
        }
        _introRunning = false;
    }

    public void SetIdleAuto()
    {
        // während Intro: nicht überschreiben lassen
        if (_introRunning) return;

        _mode = Mode.IdleAuto;
        StopTimed();

        // Nach Intro wollen wir erstmal LEER bleiben, bis Proximity/Gate was setzt
        // (für den Startzustand; nach Switch nutzt ihr SetIdlePerspective() für "Talk to me")
        if (_introFinished && swapManager != null && swapManager.EnteredCount <= 1)
        {
            ClearText();
            return;
        }

        SetText(ResolveIdleText());
    }

    public void SetIdlePerspective()
    {
        if (_introRunning) return;

        _mode = Mode.IdleAuto;
        StopTimed();
        SetText(perspectiveIdleText);
    }

    public void SetPrompt(string prompt)
    {
        CancelIntroIfNeeded();

        _mode = Mode.Prompt;
        StopTimed();
        SetText(prompt);
    }

    public void SetSticky(string t)
    {
        CancelIntroIfNeeded();

        _mode = Mode.StickyUntilReset;
        StopTimed();
        SetText(t);
    }

    public void ClearSticky()
    {
        if (_mode == Mode.StickyUntilReset)
            SetIdleAuto();
    }

    public void SetFXOverride(string t)
    {
        CancelIntroIfNeeded();

        _mode = Mode.FXOverride;
        StopTimed();
        SetText(t);
    }

    public void ClearFXOverride()
    {
        if (_mode == Mode.FXOverride)
            SetIdleAuto();
    }

    /// <summary>
    /// Kompatibilität: DialogueManager erwartet diese Methode.
    /// Zeigt den Text für X Sekunden und geht dann zurück auf IdleAuto
    /// (aber NICHT, wenn FX/Sticky inzwischen übernommen hat).
    /// </summary>
    public void SetNpcTimed(string t, float seconds)
    {
        CancelIntroIfNeeded();
        if (IsLockedByFX) return;
        if (IsSticky) return;

        _mode = Mode.Prompt;
        SetText(t);

        StopTimed();
        _timedRoutine = StartCoroutine(CoTimedReturn(seconds));
    }

    /// <summary>
    /// Macht HUD explizit leer (z.B. nach Intro oder wenn ihr resetten wollt).
    /// </summary>
    public void ClearText()
    {
        StopTimed();
        if (textTMP != null) textTMP.text = "";
    }

    // =========================================================
    // Internals
    // =========================================================

    void CancelIntroIfNeeded()
    {
        if (_introRunning)
        {
            StopIntro();
            _introFinished = true;
        }
    }

    IEnumerator CoIntro()
    {
        _introRunning = true;
        _mode = Mode.Intro;

        if (introTexts == null || introTexts.Length == 0)
        {
            _introRunning = false;
            _introFinished = true;
            ClearText();
            yield break;
        }

        for (int i = 0; i < introTexts.Length; i++)
        {
            SetText(introTexts[i]);
            yield return new WaitForSeconds(introSeconds);

            // falls Intro zwischendurch abgebrochen wurde
            if (!_introRunning) yield break;
        }

        _introRunning = false;
        _introFinished = true;

        // danach: NICHT Idle-Text, sondern leer
        ClearText();
        _mode = Mode.IdleAuto;
    }

    IEnumerator CoTimedReturn(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (!IsLockedByFX && !IsSticky && !_introRunning)
            SetIdleAuto();
    }

    void StopTimed()
    {
        if (_timedRoutine != null)
        {
            StopCoroutine(_timedRoutine);
            _timedRoutine = null;
        }
    }

    string ResolveIdleText()
    {
        if (!swapManager) return initialIdleText;
        return (swapManager.EnteredCount <= 1) ? initialIdleText : perspectiveIdleText;
    }

    void SetText(string t)
    {
        if (debugLogs) Debug.Log($"[HUD] {_mode}: {t}");
        if (textTMP != null) textTMP.text = t;
    }
}