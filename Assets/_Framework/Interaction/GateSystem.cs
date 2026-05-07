using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.Dialogue;
using Reach.Framework.HUD;

namespace Reach.Framework.Interaction
{
    /// <summary>
    /// Default gate system implementation.
    /// Place one in the scene; it self-registers with GameContext.
    /// </summary>
    public class GateSystem : MonoBehaviour, IGateSystem
    {
        [Header("Detection")]
        [Tooltip("How close the player must be to an unvisited character for the gate to be available.")]
        public float gateRadius = 2.5f;

        [Tooltip("Hysteresis multiplier on gateRadius for cancel-on-leave (e.g. 1.2 means leave at radius * 1.2).")]
        [Range(1.0f, 2.0f)]
        public float leaveHysteresis = 1.2f;

        [Tooltip("If true: cancel the gate when player walks too far from the target.")]
        public bool cancelOnLeaveRadius = true;

        [Header("Passphrase")]
        [Tooltip("Seconds before the gate auto-cancels if the player never speaks. 0 = no timeout.")]
        public float passphraseTimeoutSeconds = 12f;

        [Header("Cooldowns")]
        [Tooltip("Seconds between gate triggers (anti-spam).")]
        public float gateTriggerCooldown = 0.35f;

        [Header("HUD Texts")]
        [TextArea(1, 3)] public string promptAfterGateSpoken = "Press Speak.";
        [TextArea(1, 3)] public string promptNoMatch = "No match — try again.";
        public float afterGatePromptDelaySeconds = 0.25f;

        [Header("Outreach Lock")]
        [Tooltip("If true: player can't reach out until they've completed their character's interact action.")]
        public bool useOutreachLock = true;
        [TextArea(1, 3)] public string outreachLockedMessage = "Finish your task before reaching out.";
        public float outreachLockedMessageSeconds = 1.6f;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        PossessableCharacter _nearestTarget;
        PossessableCharacter _activeTarget;

        bool _gateTtsPlaying;
        bool _waitingForPassphrase;
        bool _timeoutSuspended;
        float _passphraseWaitStartTime = -999f;
        float _lastGateStartTime = -999f;

        int _gateRunId;
        Coroutine _afterGatePromptRoutine;

        public PossessableCharacter NearestTarget => _nearestTarget;
        public bool HasTargetInRange => _nearestTarget != null;
        public bool IsGateBusy => _gateTtsPlaying || _waitingForPassphrase;
        public bool IsWaitingForPassphrase => _activeTarget != null && _waitingForPassphrase;
        public bool ShouldBlockChat => IsGateBusy;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Awake()
        {
            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                Debug.LogError("[GateSystem] No GameContext.Instance found.");
                enabled = false;
                return;
            }
            ctx.Gate = this;
        }

        void OnEnable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.Switched += OnSwitched;
        }

        void OnDisable()
        {
            var pm = GameContext.Instance?.Perspective;
            if (pm != null) pm.Switched -= OnSwitched;
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            CancelGate();
            GameContext.Instance?.Hud?.ForceResetToIdle();
        }

        // ============================================================
        // Per-frame target detection
        // ============================================================

        void Update()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            var pm = ctx.Perspective;
            if (pm == null || pm.Current == null) return;

            // Find nearest unvisited valid character within radius
            _nearestTarget = FindNearestUnvisited(pm.Current.transform.position, gateRadius);

            // Passphrase timeout (only while actually waiting and not suspended)
            if (_waitingForPassphrase && !_timeoutSuspended && _activeTarget != null)
            {
                if (passphraseTimeoutSeconds > 0f &&
                    _passphraseWaitStartTime > 0f &&
                    Time.time - _passphraseWaitStartTime > passphraseTimeoutSeconds)
                {
                    if (debugLogs) Debug.Log("[GateSystem] Passphrase timeout -> cancel.");
                    CancelGate();
                    ctx.Hud?.ForceResetToIdle();
                }
            }

            // Cancel-on-leave with hysteresis (only when not actively recording or waiting)
            if (cancelOnLeaveRadius && _activeTarget != null && !_timeoutSuspended && !_waitingForPassphrase)
            {
                float dist = Vector3.Distance(pm.Current.transform.position, _activeTarget.transform.position);
                if (dist > gateRadius * leaveHysteresis)
                {
                    if (debugLogs) Debug.Log($"[GateSystem] Left radius -> cancel (dist={dist:0.00})");
                    CancelGate();
                    ctx.Hud?.ForceResetToIdle();
                }
            }
        }

        PossessableCharacter FindNearestUnvisited(Vector3 from, float radius)
        {
            var ctx = GameContext.Instance;
            if (ctx == null || ctx.Perspective == null) return null;

            PossessableCharacter best = null;
            float bestSqr = radius * radius;

            var current = ctx.Perspective.Current;
            foreach (var c in ctx.Characters.All)
            {
                if (c == null || !c.IsValid) continue;
                if (c == current) continue;
                if (ctx.Perspective.HasVisited(c)) continue;

                float sqr = (c.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        // ============================================================
        // Public API
        // ============================================================

        public bool TryTriggerGate()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return false;
            if (_nearestTarget == null) return false;

            if (!IsOutreachAllowed())
            {
                ctx.Hud?.SetTimed(outreachLockedMessage, outreachLockedMessageSeconds);
                if (debugLogs) Debug.Log("[GateSystem] Trigger refused: outreach locked.");
                return true; // consumed
            }

            if (IsGateBusy)
            {
                if (debugLogs) Debug.Log("[GateSystem] Trigger refused: busy.");
                return true;
            }

            if (Time.time - _lastGateStartTime < gateTriggerCooldown)
            {
                if (debugLogs) Debug.Log("[GateSystem] Trigger refused: cooldown.");
                return true;
            }

            _lastGateStartTime = Time.time;
            _ = StartGateAsync(_nearestTarget);
            return true;
        }

        bool IsOutreachAllowed()
        {
            if (!useOutreachLock) return true;

            var pm = GameContext.Instance?.Perspective;
            if (pm == null || pm.Current == null) return true;

            // Initial perspective: always allow (player just started)
            if (pm.VisitedCount <= 1)
                return true;

            // Look for the current character's interact object
            var current = pm.Current;
            var interact = FindCurrentInteractObject(current);
            if (interact == null) return true; // no object = no lock

            return interact.HasUnlockedOutreach;
        }

        InteractableObject FindCurrentInteractObject(PossessableCharacter character)
        {
            // Find the InteractableObject whose ownerCharacter is this character.
            var all = FindObjectsOfType<InteractableObject>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].ownerCharacter == character)
                    return all[i];
            return null;
        }

        // ============================================================
        // Gate flow (async)
        // ============================================================

        async Task StartGateAsync(PossessableCharacter target)
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            int myRun = ++_gateRunId;
            _activeTarget = target;
            _gateTtsPlaying = true;
            _waitingForPassphrase = false;
            _timeoutSuspended = false;

            var def = target.Definition;
            var hud = ctx.Hud;

            // Show the gate line as sticky text while TTS plays
            if (hud != null && hud.IsFree)
                hud.SetSticky(def != null ? def.gateTtsLine : "(no gate line)");

            // TTS via SpeechSystem
            var tts = ctx.Speech?.TTS;
            AudioClip clip = null;
            if (tts != null && def != null)
            {
                string voice = ResolveVoice(def);
                clip = await tts.SynthesizeAsync(def.gateTtsLine, voice);
            }

            // Run still valid?
            if (myRun != _gateRunId) return;
            if (_activeTarget != target) { _gateTtsPlaying = false; return; }

            // Play clip via the target's ambient source as a one-shot? Or a temp source.
            // For simplicity here: use a transient source on the target.
            if (clip != null)
            {
                var src = GetOrCreateGateAudioSource(target);
                src.PlayOneShot(clip);
            }

            _gateTtsPlaying = false;
            _waitingForPassphrase = true;
            _passphraseWaitStartTime = -999f; // set after audio finishes (in CoAfterGatePrompt)

            if (_afterGatePromptRoutine != null) StopCoroutine(_afterGatePromptRoutine);
            _afterGatePromptRoutine = StartCoroutine(CoAfterGatePrompt(target, clip));

            if (debugLogs) Debug.Log("[GateSystem] Gate line played -> waiting for passphrase.");
        }

        IEnumerator CoAfterGatePrompt(PossessableCharacter target, AudioClip clip)
        {
            // Wait until clip finishes (or fall back to clip.length)
            if (clip != null)
            {
                yield return new WaitForSeconds(clip.length);
            }

            if (afterGatePromptDelaySeconds > 0f)
                yield return new WaitForSeconds(afterGatePromptDelaySeconds);

            if (_activeTarget != target) yield break;
            if (!_waitingForPassphrase) yield break;

            _passphraseWaitStartTime = Time.time;

            var hud = GameContext.Instance?.Hud;
            if (hud != null && hud.IsFree)
                hud.SetSticky(promptAfterGateSpoken);
        }

        public async Task<bool> TryHandlePassphraseAsync(string wavPath)
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return false;
            if (_activeTarget == null || !_waitingForPassphrase || _gateTtsPlaying) return false;

            var stt = ctx.Speech?.STT;
            if (stt == null)
            {
                Debug.LogWarning("[GateSystem] No STT backend available.");
                return false;
            }

            string lang = ctx.pack != null ? ctx.pack.language : "en";
            string spoken = await stt.TranscribeAsync(wavPath, lang);

            var def = _activeTarget.Definition;
            string expected = def != null ? def.gatePassphrase : "";
            float threshold = def != null ? def.gateSimilarityThreshold : 0.82f;

            bool match = StringSimilarity.Matches(spoken, expected, threshold);

            if (!match)
            {
                if (debugLogs) Debug.Log($"[GateSystem] No match. Heard='{spoken}' Expected='{expected}'");

                var hud = ctx.Hud;
                if (hud != null && hud.IsFree)
                    hud.SetSticky(promptNoMatch);

                if (_passphraseWaitStartTime > 0f)
                    _passphraseWaitStartTime = Time.time;

                return true; // consumed
            }

            // Match → switch
            var target = _activeTarget;
            _waitingForPassphrase = false;

            bool switched = ctx.Perspective.TrySwitchTo(target);
            if (debugLogs) Debug.Log($"[GateSystem] MATCH '{spoken}' → switch={switched}");

            CancelGate(); // cleanup
            ctx.Hud?.ForceResetToIdle();
            return true;
        }

        // ============================================================
        // Cancel + suspend
        // ============================================================

        public void CancelGate()
        {
            _gateRunId++;

            if (_afterGatePromptRoutine != null)
            {
                StopCoroutine(_afterGatePromptRoutine);
                _afterGatePromptRoutine = null;
            }

            _activeTarget = null;
            _waitingForPassphrase = false;
            _gateTtsPlaying = false;
            _timeoutSuspended = false;
            _passphraseWaitStartTime = -999f;
            _lastGateStartTime = -999f;

            if (debugLogs) Debug.Log("[GateSystem] CancelGate");
        }

        public void SetTimeoutSuspended(bool suspended)
        {
            _timeoutSuspended = suspended;

            // When un-suspending, restart the timeout window from now.
            if (!suspended && _waitingForPassphrase && _passphraseWaitStartTime > 0f)
                _passphraseWaitStartTime = Time.time;
        }

        // ============================================================
        // Helpers
        // ============================================================

        AudioSource GetOrCreateGateAudioSource(PossessableCharacter target)
        {
            // Use a dedicated child AudioSource so it doesn't interfere with ambient.
            var go = target.gameObject;
            var src = go.GetComponent<GateVoiceSource>();
            if (src == null)
            {
                src = go.AddComponent<GateVoiceSource>();
                src.Init();
            }
            return src.Source;
        }

        string ResolveVoice(CharacterDefinition def)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return def.voiceMacOS;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return def.voiceWindows;
#else
            return def.voiceMacOS;
#endif
        }
    }

    /// <summary>
    /// Tiny holder so we don't pollute the character with another raw AudioSource.
    /// Created on demand by GateSystem.
    /// </summary>
    [DisallowMultipleComponent]
    public class GateVoiceSource : MonoBehaviour
    {
        public AudioSource Source { get; private set; }

        public void Init()
        {
            Source = gameObject.AddComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = false;
            Source.spatialBlend = 1f; // 3D
        }
    }
}