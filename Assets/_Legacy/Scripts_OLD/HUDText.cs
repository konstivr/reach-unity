// HUDText.cs (DROP-IN REPLACE)
// Improvements for "no fighting":
// ✅ Clear and explicit modes + lock flags.
// ✅ IsTimedLocked is true while timed routine exists (already).
// ✅ ClearSticky() returns to IdleAuto (Router owns what IdleAuto becomes via ResolveIdleText).
// ✅ ForceResetToIdle does not keep old sticky/timed/intro.
// ✅ CoTimedReturn sets _timedRoutine=null AFTER the idle restoration (so IsTimedLocked is accurate).
// ✅ Router can safely check: !IsLockedByFX && !IsSticky && !IsTimedLocked && !IsIntroRunning

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
    public bool playIntroOnStart = true;

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

    Coroutine _timedRoutine;

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
    public bool IsTimedLocked => _timedRoutine != null;

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
        if (_introRunning) return;

        _mode = Mode.IdleAuto;
        StopTimed();

        // After intro: leave empty until something else sets a prompt (e.g., router proximity)
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

    public void ClearText()
    {
        StopTimed();
        if (textTMP != null) textTMP.text = "";
    }

    public void ForceResetToIdle()
    {
        StopIntro();
        _introFinished = true;

        StopTimed();
        _mode = Mode.IdleAuto;

        SetIdleAuto();

        if (debugLogs) Debug.Log("[HUD] ForceResetToIdle()");
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

            if (!_introRunning) yield break;
        }

        _introRunning = false;
        _introFinished = true;

        ClearText();
        _mode = Mode.IdleAuto;
    }

    IEnumerator CoTimedReturn(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        // unlock first so IsTimedLocked becomes false when SetIdleAuto checks it elsewhere
        _timedRoutine = null;

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