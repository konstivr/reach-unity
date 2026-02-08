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

    [TextArea(1, 3)]
    public string outreachLockedMessage = "Finish your tasks (and talk once) before reaching out again.";
    public float outreachLockedMessageSeconds = 1.6f;

    [Header("Gate")]
    public float gateRadius = 2.5f;
    public bool cancelGateWhenLeavingRadius = true;
    [Range(1.0f, 2.0f)] public float leaveHysteresis = 1.2f;

    [Header("Anti Spam")]
    public float gateTriggerCooldown = 0.35f;

    [Header("Debug")]
    public bool debugLogs = true;

    DialogueAgent _nearestGateAgent;
    DialogueAgent _activeGateAgent;

    bool _waitingForPassphrase = false;
    bool _gateTtsPlaying = false;

    float _lastGateStartTime = -999f;

    public DialogueAgent NearestGateAgent => _nearestGateAgent;
    public bool IsWaitingForPassphrase => _activeGateAgent != null && _waitingForPassphrase;
    public bool IsGateBusy => _gateTtsPlaying || IsWaitingForPassphrase;
    public bool HasGateTargetInRange => _nearestGateAgent != null;

    public bool IsInGateZone => HasGateTargetInRange || IsWaitingForPassphrase;

    public bool ShouldBlockChat()
    {
        if (_gateTtsPlaying || IsWaitingForPassphrase) return true;
        if (!IsOutreachAllowed()) return false;
        return HasGateTargetInRange;
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
        ResetGateState(stopSpeaking: true);
        if (hud) hud.ClearSticky();
        _lastGateStartTime = -999f;
    }

    void Update()
    {
        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;
        if (transitionFX != null && transitionFX.IsTransitioning) return;

        var currentChar = swapManager.current;
        var inputs = currentChar.inputs;

        bool outreachAllowed = IsOutreachAllowed();

        _nearestGateAgent = outreachAllowed
            ? FindNearestUnvisitedAgent(currentChar.transform.position, gateRadius)
            : null;

        // cancel when leaving
        if (cancelGateWhenLeavingRadius && _activeGateAgent != null)
        {
            float dist = Vector3.Distance(currentChar.transform.position, _activeGateAgent.transform.position);
            if (dist > gateRadius * leaveHysteresis)
            {
                if (debugLogs) Debug.Log($"[Gate] Left radius -> cancel (dist={dist:0.00})");
                ResetGateState(stopSpeaking: true);
                if (hud) hud.ClearSticky();
            }
        }

        // ✅ IMPORTANT: use EDGE, not held
        if (!inputs.dialogueStartPressed) return;

        if (!outreachAllowed)
        {
            if (hud != null && !hud.IsLockedByFX)
                hud.SetNpcTimed(outreachLockedMessage, outreachLockedMessageSeconds);

            if (debugLogs) Debug.Log("[Gate] Outreach locked -> ignore.");
            return;
        }

        if (_gateTtsPlaying || _waitingForPassphrase)
        {
            if (debugLogs) Debug.Log("[Gate] Busy/waiting -> ignore trigger.");
            return;
        }

        if (Time.time - _lastGateStartTime < gateTriggerCooldown)
        {
            if (debugLogs) Debug.Log("[Gate] Cooldown -> ignore trigger.");
            return;
        }

        if (_nearestGateAgent == null) return;

        _lastGateStartTime = Time.time;
        StartGateFor(_nearestGateAgent);
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

        _activeGateAgent = agent;
        _gateTtsPlaying = true;
        _waitingForPassphrase = false;

        if (hud) hud.SetSticky(agent.gateTtsLine);

        AudioClip clip = await speechOutput.TextToSpeech(agent.gateTtsLine);

        if (_activeGateAgent != agent)
        {
            _gateTtsPlaying = false;
            return;
        }

        if (clip != null)
            agent.Speak(clip);

        _gateTtsPlaying = false;
        _waitingForPassphrase = true;

        if (debugLogs) Debug.Log("[Gate] Gate line played -> waiting for passphrase.");
    }

    public async Task<bool> TryHandleGatePassphrase(string wavPath)
    {
        if (transitionFX != null && transitionFX.IsTransitioning) return false;
        if (_activeGateAgent == null || !_waitingForPassphrase || _gateTtsPlaying) return false;
        if (!whisperSTT) return false;

        string stt = await whisperSTT.TranscribeWav(wavPath);

        bool ok = StringSimilarity.Matches(stt, _activeGateAgent.gatePassphrase, _activeGateAgent.gateSimilarityThreshold);
        if (!ok) return true;

        var target = _activeGateAgent.owner;
        _waitingForPassphrase = false;

        bool switched = false;
        if (transitionFX != null) switched = await transitionFX.PlayReachAndSwitch(target);
        else switched = swapManager != null && swapManager.TrySwitchTo(target);

        if (!switched && debugLogs) Debug.Log("[Gate] Switch returned false.");

        ResetGateState(stopSpeaking: false);
        if (hud) hud.ClearSticky();
        return true;
    }

    void ResetGateState(bool stopSpeaking)
    {
        if (stopSpeaking && _activeGateAgent != null) _activeGateAgent.StopSpeaking();
        _activeGateAgent = null;
        _waitingForPassphrase = false;
        _gateTtsPlaying = false;
    }

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