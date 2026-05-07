using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class InteractionGateProximity : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public SpeechOutput speechOutput;
    public WhisperSTT whisperSTT;
    public HUDText hud;
    public ReachTransitionFX transitionFX;

    [Header("Quest / Outreach Lock")]
    public bool useQuestOutreachLock = true;
    [TextArea(1, 3)] public string outreachLockedMessage = "Finish your tasks (and talk once) before reaching out again.";
    public float outreachLockedMessageSeconds = 1.6f;

    [Header("Gate")]
    public float gateRadius = 2.5f;
    public bool cancelGateWhenLeavingRadius = true;
    [Range(1.0f, 2.0f)] public float leaveHysteresis = 1.2f;

    [Header("Passphrase Waiting")]
    public float passphraseWaitTimeoutSeconds = 12f;

    [Header("Anti Spam")]
    public float gateTriggerCooldown = 0.35f;

    [Header("HUD Texts (Gate-owned)")]
    [TextArea(1, 3)] public string promptAfterGateSpoken = "Speak by clicking the right Button once.";
    [TextArea(1, 3)] public string promptNoMatch = "No match — try again. Click to speak.";
    public float afterGatePromptDelay = 0.25f;

    [Header("Debug")]
    public bool debugLogs = true;

    DialogueAgent _nearestGateAgent;
    DialogueAgent _activeGateAgent;

    PossessableCharacter _gateFrozenTarget;

    bool _waitingForPassphrase = false;
    bool _gateTtsPlaying = false;
    float _lastGateStartTime = -999f;

    float _passphraseWaitStartTime = -999f;
    bool _timeoutSuspended = false;

    Coroutine _afterGatePromptRoutine;

    int _gateRunId = 0;

    public DialogueAgent NearestGateAgent => _nearestGateAgent;
    public bool IsWaitingForPassphrase => _activeGateAgent != null && _waitingForPassphrase;
    public bool IsGateBusy => _gateTtsPlaying || IsWaitingForPassphrase;
    public bool HasGateTargetInRange => _nearestGateAgent != null;

    public bool ShouldBlockChat()
    {
        return _gateTtsPlaying || IsWaitingForPassphrase;
    }

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!speechOutput) speechOutput = FindObjectOfType<SpeechOutput>();
        if (!whisperSTT) whisperSTT = FindObjectOfType<WhisperSTT>();
        if (!hud) hud = FindObjectOfType<HUDText>();
        if (!transitionFX) transitionFX = FindObjectOfType<ReachTransitionFX>();
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        CancelGate(resetCooldown: true, stopSpeaking: true);
        // Router will set idle next frame; we just hard reset HUD if needed
        if (hud) hud.ForceResetToIdle();
    }

    void Update()
    {
        if (!swapManager || !swapManager.current) return;
        if (transitionFX != null && transitionFX.IsTransitioning) return;

        var currentChar = swapManager.current;

        // Always update nearest target
        _nearestGateAgent = FindNearestUnvisitedAgent(currentChar.transform.position, gateRadius);

        // Timeout only while waiting, not suspended
        if (_waitingForPassphrase && _activeGateAgent != null && !_timeoutSuspended)
        {
            if (passphraseWaitTimeoutSeconds > 0f &&
                _passphraseWaitStartTime > 0f &&
                Time.time - _passphraseWaitStartTime > passphraseWaitTimeoutSeconds)
            {
                if (debugLogs) Debug.Log("[Gate] Passphrase timeout -> cancel.");
                CancelGate(resetCooldown: true, stopSpeaking: true);
                if (hud) hud.ForceResetToIdle();
            }
        }

        // Leaving radius cancels (but not while recording or waiting)
        if (cancelGateWhenLeavingRadius && _activeGateAgent != null)
        {
            if (_timeoutSuspended || _waitingForPassphrase) return;

            float dist = Vector3.Distance(currentChar.transform.position, _activeGateAgent.transform.position);
            if (dist > gateRadius * leaveHysteresis)
            {
                if (debugLogs) Debug.Log($"[Gate] Left radius -> cancel (dist={dist:0.00})");
                CancelGate(resetCooldown: true, stopSpeaking: true);
                if (hud) hud.ForceResetToIdle();
            }
        }
    }

    public bool TryTriggerGateFromInput()
    {
        if (!swapManager || swapManager.current == null) return false;
        if (transitionFX != null && transitionFX.IsTransitioning) return false;

        if (_nearestGateAgent == null)
        {
            if (debugLogs) Debug.Log("[Gate] Trigger: no target in range -> NOT consumed.");
            return false;
        }

        bool outreachAllowed = IsOutreachAllowed();
        if (!outreachAllowed)
        {
            if (hud != null && !hud.IsLockedByFX && !hud.IsIntroRunning)
                hud.SetNpcTimed(outreachLockedMessage, outreachLockedMessageSeconds);

            if (debugLogs) Debug.Log("[Gate] Trigger: outreach locked -> consumed.");
            return true;
        }

        if (_gateTtsPlaying || _waitingForPassphrase)
        {
            if (debugLogs) Debug.Log("[Gate] Trigger: busy/waiting -> consumed.");
            return true;
        }

        if (Time.time - _lastGateStartTime < gateTriggerCooldown)
        {
            if (debugLogs) Debug.Log("[Gate] Trigger: cooldown -> consumed.");
            return true;
        }

        _lastGateStartTime = Time.time;
        StartGateFor(_nearestGateAgent);
        return true;
    }

    bool IsOutreachAllowed()
    {
        if (!useQuestOutreachLock) return true;
        if (QuestStateManager.Instance == null) return true;
        if (swapManager == null || swapManager.current == null) return true;
        return QuestStateManager.Instance.CanOutreachFrom(swapManager.current);
    }

    async void StartGateFor(DialogueAgent agent)
    {
        if (!agent || !agent.owner) return;
        if (!speechOutput) return;
        if (_gateTtsPlaying || _waitingForPassphrase) return;

        int myRun = ++_gateRunId;

        _activeGateAgent = agent;

        // Freeze target during gate
        _gateFrozenTarget = agent.owner;
        if (_gateFrozenTarget != null)
            _gateFrozenTarget.SetExternalFrozen(true, "gate");

        _gateTtsPlaying = true;
        _waitingForPassphrase = false;
        _timeoutSuspended = false;

        // Gate owns its TTS line display
        if (hud != null && !hud.IsLockedByFX && !hud.IsIntroRunning)
            hud.SetSticky(agent.gateTtsLine);

        AudioClip clip = await speechOutput.TextToSpeech(agent.gateTtsLine);

        // invalidate stale async returns (cancel/new run)
        if (myRun != _gateRunId) return;

        if (_activeGateAgent != agent)
        {
            _gateTtsPlaying = false;
            if (debugLogs) Debug.Log("[Gate] TTS finished but gate was canceled -> abort.");
            return;
        }

        if (clip != null)
            agent.Speak(clip);

        _gateTtsPlaying = false;

        _waitingForPassphrase = true;
        _passphraseWaitStartTime = -999f;

        if (_afterGatePromptRoutine != null) StopCoroutine(_afterGatePromptRoutine);
        _afterGatePromptRoutine = StartCoroutine(CoAfterGatePrompt(agent, clip));

        if (debugLogs) Debug.Log("[Gate] Gate line played -> waiting for passphrase.");
    }

    IEnumerator CoAfterGatePrompt(DialogueAgent agent, AudioClip clip)
    {
        if (agent != null && agent.voiceSource != null)
        {
            yield return null;
            float safety = 0f;
            while (agent.voiceSource.isPlaying && safety < 30f)
            {
                safety += Time.deltaTime;
                yield return null;
            }
        }
        else if (clip != null)
        {
            yield return new WaitForSeconds(clip.length);
        }

        if (afterGatePromptDelay > 0f)
            yield return new WaitForSeconds(afterGatePromptDelay);

        if (_activeGateAgent != agent) yield break;
        if (!_waitingForPassphrase) yield break;

        _passphraseWaitStartTime = Time.time;

        if (hud != null && !hud.IsLockedByFX && !hud.IsIntroRunning)
            hud.SetSticky(promptAfterGateSpoken);
    }

    public async Task<bool> TryHandleGatePassphrase(string wavPath)
    {
        if (transitionFX != null && transitionFX.IsTransitioning) return false;
        if (_activeGateAgent == null || !_waitingForPassphrase || _gateTtsPlaying) return false;
        if (!whisperSTT) return false;

        string stt = await whisperSTT.TranscribeWav(wavPath);

        bool ok = StringSimilarity.Matches(stt, _activeGateAgent.gatePassphrase, _activeGateAgent.gateSimilarityThreshold);

        if (!ok)
        {
            if (hud != null && !hud.IsLockedByFX && !hud.IsIntroRunning)
                hud.SetSticky(promptNoMatch);

            if (_passphraseWaitStartTime > 0f)
                _passphraseWaitStartTime = Time.time;

            if (debugLogs) Debug.Log($"[Gate] NO match -> keep waiting. stt='{stt}'");
            return true;
        }

        var target = _activeGateAgent.owner;
        _waitingForPassphrase = false;

        bool switched = false;
        if (transitionFX != null) switched = await transitionFX.PlayReachAndSwitch(target);
        else switched = swapManager != null && swapManager.TrySwitchTo(target);

        if (!switched && debugLogs) Debug.Log("[Gate] Switch returned false.");

        // release freeze on success
        if (_gateFrozenTarget != null)
        {
            _gateFrozenTarget.SetExternalFrozen(false, "gate");
            _gateFrozenTarget = null;
        }

        CancelGate(resetCooldown: true, stopSpeaking: false);

        // Gate is done; Router will set the right idle/prompt
        if (hud) hud.ForceResetToIdle();

        return true;
    }

    // =========================================================
    // Cancel + timeout suspend
    // =========================================================

    public void CancelGate()
    {
        CancelGate(resetCooldown: true, stopSpeaking: true);
    }

    public void CancelGate(bool resetCooldown, bool stopSpeaking)
    {
        if (debugLogs) Debug.Log("[Gate] CancelGate()");

        // invalidate async completions
        _gateRunId++;

        if (_afterGatePromptRoutine != null)
        {
            StopCoroutine(_afterGatePromptRoutine);
            _afterGatePromptRoutine = null;
        }

        if (stopSpeaking && _activeGateAgent != null)
            _activeGateAgent.StopSpeaking();

        _activeGateAgent = null;
        _waitingForPassphrase = false;
        _gateTtsPlaying = false;
        _timeoutSuspended = false;
        _passphraseWaitStartTime = -999f;

        if (resetCooldown)
            _lastGateStartTime = -999f;

        // ALWAYS release freeze when canceled
        if (_gateFrozenTarget != null)
        {
            _gateFrozenTarget.SetExternalFrozen(false, "gate");
            _gateFrozenTarget = null;
        }

        // Do not set idle prompt here; Router owns that
    }

    public void SetTimeoutSuspended(bool suspended)
    {
        _timeoutSuspended = suspended;

        if (!suspended && _waitingForPassphrase && _passphraseWaitStartTime > 0f)
            _passphraseWaitStartTime = Time.time;

        if (debugLogs) Debug.Log($"[Gate] SetTimeoutSuspended({suspended})");
    }

    // =========================================================

    DialogueAgent FindNearestUnvisitedAgent(Vector3 playerPos, float radius)
    {
        var agents = FindObjectsOfType<DialogueAgent>();
        DialogueAgent best = null;
        float bestDistSqr = float.MaxValue;
        float rSqr = radius * radius;

        foreach (var a in agents)
        {
            if (!a || !a.gameObject.activeInHierarchy) continue;
            if (!a.owner || !a.owner.IsValid) continue;
            if (swapManager && swapManager.current && a.owner == swapManager.current) continue;
            if (swapManager && swapManager.HasVisited(a.owner)) continue;

            float dSqr = (a.transform.position - playerPos).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = a;
            }
        }
        return best;
    }

    static class StringSimilarity
    {
        public static bool Matches(string a, string b, float threshold)
        {
            string x = Normalize(a);
            string y = Normalize(b);
            if (string.IsNullOrEmpty(x) || string.IsNullOrEmpty(y)) return false;

            if (x == y) return true;
            if (x.Contains(y) || y.Contains(x)) return true;

            float sim = Similarity01(x, y);
            return sim >= Mathf.Clamp01(threshold);
        }

        static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.ToLowerInvariant().Trim();
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) sb.Append(c);
            }
            return CollapseSpaces(sb.ToString());
        }

        static string CollapseSpaces(string s)
        {
            var sb = new StringBuilder(s.Length);
            bool lastSpace = false;
            foreach (char c in s)
            {
                bool space = char.IsWhiteSpace(c);
                if (space)
                {
                    if (!lastSpace) sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }
            return sb.ToString().Trim();
        }

        static float Similarity01(string s1, string s2)
        {
            int dist = LevenshteinDistance(s1, s2);
            int maxLen = Mathf.Max(s1.Length, s2.Length);
            if (maxLen == 0) return 1f;
            return 1f - (float)dist / maxLen;
        }

        static int LevenshteinDistance(string s, string t)
        {
            int n = s.Length, m = t.Length;
            if (n == 0) return m;
            if (m == 0) return n;

            int[] prev = new int[m + 1];
            int[] curr = new int[m + 1];
            for (int j = 0; j <= m; j++) prev[j] = j;

            for (int i = 1; i <= n; i++)
            {
                curr[0] = i;
                char sc = s[i - 1];

                for (int j = 1; j <= m; j++)
                {
                    int cost = (sc == t[j - 1]) ? 0 : 1;
                    int del = prev[j] + 1;
                    int ins = curr[j - 1] + 1;
                    int sub = prev[j - 1] + cost;
                    curr[j] = Mathf.Min(del, Mathf.Min(ins, sub));
                }
                var tmp = prev; prev = curr; curr = tmp;
            }
            return prev[m];
        }
    }
}