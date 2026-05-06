// DialogueManager.cs
// HUD zeigt Ollama-Output SOLANGE gesprochen wird (AudioSource.isPlaying), nicht per Timeout.

using System.Collections.Generic;
using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("State")]
    public DialogueState state = DialogueState.None;

    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public InteractionGateProximity gate;
    public OllamaClient ollama;
    public SpeechOutput speechOutput;
    public WhisperSTT whisperSTT;
    public HUDText hud;

    [Header("Conversation")]
    public int maxHistoryMessages = 14;

    [Header("Debug")]
    public bool debugLogs = true;

    readonly List<OllamaClient.ChatMessage> _messages = new List<OllamaClient.ChatMessage>();
    DialogueAgent _currentPlayerAgent;

    Coroutine _hudSpeakRoutine;
    int _speakToken = 0; // cancels stale coroutines safely

    void Awake()
    {
        Instance = this;

        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!gate) gate = FindObjectOfType<InteractionGateProximity>();
        if (!ollama) ollama = FindObjectOfType<OllamaClient>();
        if (!speechOutput) speechOutput = FindObjectOfType<SpeechOutput>();
        if (!whisperSTT) whisperSTT = FindObjectOfType<WhisperSTT>();
        if (!hud) hud = FindObjectOfType<HUDText>();

        if (debugLogs)
            Debug.Log($"[DialogueManager] Awake | swap={(swapManager ? "OK" : "NULL")} gate={(gate ? "OK" : "NULL")} ollama={(ollama ? "OK" : "NULL")} tts={(speechOutput ? "OK" : "NULL")} stt={(whisperSTT ? "OK" : "NULL")} hud={(hud ? "OK" : "NULL")}");
    }

    void OnEnable()
    {
        if (swapManager != null) swapManager.Switched += OnSwitched;
    }

    void OnDisable()
    {
        if (swapManager != null) swapManager.Switched -= OnSwitched;
    }

    void Start()
    {
        RefreshCurrentPlayerAgent(resetHistory: true);
        state = DialogueState.Listening;

        if (hud != null)
            hud.SetIdleAuto();

        ApplyHeartsVisibilityAndResetIfNeeded(reset: false);
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        if (debugLogs) Debug.Log($"[DialogueManager] OnSwitched {from?.name} -> {to?.name} | reset chat history");

        InterruptDialogue();                 // stops speaking + cancels HUD lock
        RefreshCurrentPlayerAgent(true);
        state = DialogueState.Listening;

        if (hud != null)
            hud.SetIdlePerspective();

        ApplyHeartsVisibilityAndResetIfNeeded(reset: true);
    }

    void ApplyHeartsVisibilityAndResetIfNeeded(bool reset)
    {
        bool isDefaultPerspective = (swapManager == null) || (swapManager.EnteredCount <= 1);
        if (HUDHearts.Instance == null) return;

        if (isDefaultPerspective)
        {
            HUDHearts.Instance.Hide();
            return;
        }

        if (reset) HUDHearts.Instance.ResetHeartsAndShow();
        else HUDHearts.Instance.ResetHeartsAndShow();
    }

    void RefreshCurrentPlayerAgent(bool resetHistory)
    {
        _currentPlayerAgent = (swapManager != null) ? swapManager.CurrentAgent : null;

        if (debugLogs)
            Debug.Log($"[DialogueManager] CurrentPlayerAgent={(_currentPlayerAgent ? _currentPlayerAgent.name : "NULL")}");

        if (resetHistory)
        {
            _messages.Clear();
            if (_currentPlayerAgent)
            {
                _messages.Add(new OllamaClient.ChatMessage
                {
                    role = "system",
                    content = _currentPlayerAgent.chatSystemPrompt
                });

                if (debugLogs)
                    Debug.Log($"[DialogueManager] Set system prompt len={_currentPlayerAgent.chatSystemPrompt?.Length ?? 0}");
            }
        }
    }

    public void InterruptDialogue()
    {
        // cancel HUD coroutine + unlock HUD
        _speakToken++;
        if (_hudSpeakRoutine != null)
        {
            StopCoroutine(_hudSpeakRoutine);
            _hudSpeakRoutine = null;
        }

        if (hud != null) hud.ClearFXOverride();

        if (_currentPlayerAgent) _currentPlayerAgent.StopSpeaking();

        state = DialogueState.Interrupted;
        if (debugLogs) Debug.Log("[DialogueManager] InterruptDialogue -> state=Interrupted");
    }

    public async System.Threading.Tasks.Task PlayerSpokeFromWav(string wavPath)
    {
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

        // ✅ Nur nach Perspektiv-Switch chatten
        if (swapManager == null || swapManager.EnteredCount <= 1)
        {
            if (debugLogs) Debug.Log("[DialogueManager] Ignored chat: still in default perspective (EnteredCount <= 1).");
            return;
        }

        if (gate != null && gate.ShouldBlockChat())
        {
            if (debugLogs) Debug.Log("[DialogueManager] Chat blocked (gate context).");
            return;
        }

        if (!_currentPlayerAgent)
        {
            Debug.LogError("[DialogueManager] No current player DialogueAgent found.");
            return;
        }
        if (!whisperSTT) { Debug.LogError("[DialogueManager] WhisperSTT missing."); return; }
        if (!ollama) { Debug.LogError("[DialogueManager] OllamaClient missing."); return; }
        if (!speechOutput) { Debug.LogError("[DialogueManager] SpeechOutput missing."); return; }

        state = DialogueState.NPCResponding;

        string playerText = await whisperSTT.TranscribeWav(wavPath);
        if (debugLogs) Debug.Log($"[DialogueManager] PLAYER TEXT: '{playerText}'");

        if (string.IsNullOrWhiteSpace(playerText))
        {
            state = DialogueState.Listening;
            return;
        }

        _messages.Add(new OllamaClient.ChatMessage { role = "user", content = playerText });
        TrimHistory();

        string npcText = await ollama.ChatOnce(_messages);
        if (debugLogs) Debug.Log($"[DialogueManager] NPC TEXT:\n{npcText}");

        _messages.Add(new OllamaClient.ChatMessage { role = "assistant", content = npcText });
        TrimHistory();

        // ✅ HUD: fixen Text locken (Router darf nicht drüber)
        if (hud != null)
            hud.SetFXOverride(npcText);

        AudioClip clip = await speechOutput.TextToSpeech(npcText);

        // Speak
        _currentPlayerAgent.Speak(clip);

        // ✅ halte HUD bis Audio wirklich fertig ist
        StartHoldHudWhileSpeaking(_currentPlayerAgent, clip);

        // OPTIONAL hearts
        if (HUDHearts.Instance != null)
        {
            if (HUDHearts.Instance.GetFilled() < 3)
                HUDHearts.Instance.Advance();
        }

        state = DialogueState.Listening;
    }

    void StartHoldHudWhileSpeaking(DialogueAgent agent, AudioClip expectedClip)
    {
        _speakToken++;
        int token = _speakToken;

        if (_hudSpeakRoutine != null)
        {
            StopCoroutine(_hudSpeakRoutine);
            _hudSpeakRoutine = null;
        }

        _hudSpeakRoutine = StartCoroutine(CoHoldHudWhileSpeaking(agent, expectedClip, token));
    }

    AudioSource TryGetAgentAudioSource(DialogueAgent agent)
    {
        if (agent == null) return null;

        // Robust: falls voiceSource nicht public ist, vermeiden wir compile errors.
        // Nimm die AudioSource, die der Agent zum Sprechen nutzt (meistens am Agent oder Child).
        var src = agent.GetComponent<AudioSource>();
        if (src != null) return src;

        return agent.GetComponentInChildren<AudioSource>();
    }

    System.Collections.IEnumerator CoHoldHudWhileSpeaking(DialogueAgent agent, AudioClip expectedClip, int token)
    {
        if (hud == null)
            yield break;

        // 1 Frame warten, damit Speak() die Source/Clip gesetzt hat
        yield return null;

        if (token != _speakToken) yield break;

        AudioSource src = TryGetAgentAudioSource(agent);

        // Fallback: wenn keine Source -> warte clip.length
        if (src == null)
        {
            if (expectedClip != null)
                yield return new WaitForSeconds(Mathf.Max(0.05f, expectedClip.length));

            if (token == _speakToken) hud.ClearFXOverride();
            yield break;
        }

        while (token == _speakToken)
        {
            bool playing = src.isPlaying;

            // Wenn ein expectedClip gegeben ist, checke ob Source noch denselben Clip spielt.
            bool sameClip = (expectedClip == null) ? true : (src.clip == expectedClip);

            if (!playing || !sameClip)
                break;

            yield return null;
        }

        if (token == _speakToken)
            hud.ClearFXOverride();
    }

    void TrimHistory()
    {
        if (_messages.Count <= 1) return;

        int keep = Mathf.Max(3, maxHistoryMessages);
        int overflow = _messages.Count - (1 + keep);
        if (overflow > 0)
        {
            _messages.RemoveRange(1, overflow);
            if (debugLogs) Debug.Log($"[DialogueManager] Trim history -> msgs={_messages.Count}");
        }
    }
}