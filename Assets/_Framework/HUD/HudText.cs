using System.Collections;
using TMPro;
using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.HUD
{
    /// <summary>
    /// One text element, multiple modes, explicit locks.
    /// Place on a GameObject with a TMP_Text reference (typically a UI Canvas).
    /// </summary>
    public class HudText : MonoBehaviour, IHud
    {
        [Header("Text")]
        public TMP_Text textTMP;

        [Header("Idle Texts (fallbacks if not in pack)")]
        [TextArea(1, 3)]
        public string initialIdleText = "Reach out to interact";

        [TextArea(1, 3)]
        public string perspectiveIdleText = "Talk to me";

        [Header("Intro Sequence")]
        public bool playIntroOnStart = true;

        [TextArea(1, 3)]
        public string[] introTexts = new string[]
        {
            "Press Interact to reach out",
            "Walk closer to a person to reach out",
            "Hold the right button to speak"
        };

        [Min(0.1f)]
        public float introSecondsPerText = 5f;

        [Header("Debug")]
        public bool debugLogs = false;

        // ============================================================
        // Mode state
        // ============================================================

        public enum Mode
        {
            IdleAuto,
            Prompt,
            StickyUntilReset,
            FXOverride,
            Intro,
        }

        Mode _mode = Mode.IdleAuto;
        Coroutine _timedRoutine;
        Coroutine _introRoutine;
        bool _introRunning;
        bool _introFinished;

        public Mode CurrentMode => _mode;

        public bool IsLockedByFX => _mode == Mode.FXOverride;
        public bool IsSticky => _mode == Mode.StickyUntilReset;
        public bool IsIntroRunning => _introRunning;
        public bool IsTimedLocked => _timedRoutine != null;
        public bool IsFree => !IsLockedByFX && !IsSticky && !IsTimedLocked && !IsIntroRunning;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx != null)
                ctx.Hud = this;
        }

        void Start()
        {
            if (textTMP == null)
            {
                Debug.LogError($"[HudText] '{name}': textTMP not assigned.");
                return;
            }

            if (playIntroOnStart)
                StartIntroSequence();
            else
                ClearText();
        }

        // ============================================================
        // IHud implementation
        // ============================================================

        public void SetIdleAuto()
        {
            if (_introRunning) return;

            _mode = Mode.IdleAuto;
            StopTimed();

            // After intro: stay empty until something prompts (e.g. proximity).
            if (_introFinished && IsBeforeFirstSwitch())
            {
                ClearTextInternal();
                return;
            }

            SetTextInternal(ResolveIdleText());
        }

        public void SetIdlePerspective()
        {
            if (_introRunning) return;

            _mode = Mode.IdleAuto;
            StopTimed();
            SetTextInternal(perspectiveIdleText);
        }

        public void SetPrompt(string text)
        {
            CancelIntroIfNeeded();
            _mode = Mode.Prompt;
            StopTimed();
            SetTextInternal(text);
        }

        public void SetSticky(string text)
        {
            CancelIntroIfNeeded();
            _mode = Mode.StickyUntilReset;
            StopTimed();
            SetTextInternal(text);
        }

        public void ClearSticky()
        {
            if (_mode == Mode.StickyUntilReset)
                SetIdleAuto();
        }

        public void SetFXOverride(string text)
        {
            CancelIntroIfNeeded();
            _mode = Mode.FXOverride;
            StopTimed();
            SetTextInternal(text);
        }

        public void ClearFXOverride()
        {
            if (_mode == Mode.FXOverride)
                SetIdleAuto();
        }

        public void SetTimed(string text, float seconds)
        {
            CancelIntroIfNeeded();
            if (IsLockedByFX) return;
            if (IsSticky) return;

            _mode = Mode.Prompt;
            SetTextInternal(text);

            StopTimed();
            _timedRoutine = StartCoroutine(CoTimedReturn(seconds));
        }

        public void ForceResetToIdle()
        {
            StopIntro();
            _introFinished = true;

            StopTimed();
            _mode = Mode.IdleAuto;
            SetIdleAuto();

            if (debugLogs) Debug.Log("[HudText] ForceResetToIdle");
        }

        public void ClearText()
        {
            StopTimed();
            ClearTextInternal();
        }

        // ============================================================
        // Intro
        // ============================================================

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

        IEnumerator CoIntro()
        {
            _introRunning = true;
            _mode = Mode.Intro;

            if (introTexts == null || introTexts.Length == 0)
            {
                _introRunning = false;
                _introFinished = true;
                ClearTextInternal();
                yield break;
            }

            for (int i = 0; i < introTexts.Length; i++)
            {
                SetTextInternal(introTexts[i]);
                yield return new WaitForSeconds(introSecondsPerText);
                if (!_introRunning) yield break;
            }

            _introRunning = false;
            _introFinished = true;
            ClearTextInternal();
            _mode = Mode.IdleAuto;
        }

        void CancelIntroIfNeeded()
        {
            if (_introRunning)
            {
                StopIntro();
                _introFinished = true;
            }
        }

        // ============================================================
        // Internals
        // ============================================================

        IEnumerator CoTimedReturn(float seconds)
        {
            yield return new WaitForSeconds(seconds);
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
            return IsBeforeFirstSwitch() ? initialIdleText : perspectiveIdleText;
        }

        bool IsBeforeFirstSwitch()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm == null) return true;
            return pm.VisitedCount <= 1;
        }

        void SetTextInternal(string t)
        {
            if (debugLogs) Debug.Log($"[HudText] {_mode}: {t}");
            if (textTMP != null) textTMP.text = t ?? "";
        }

        void ClearTextInternal()
        {
            if (textTMP != null) textTMP.text = "";
        }
    }
}