// DialogueManager.cs
// -> Vollständig, inkl. Hearts-Logik:
//    - Default (EnteredCount <= 1): Hearts aus
//    - Besessen (EnteredCount > 1): Hearts an + Reset bei Switch
//    - Optional: MarkFreeTalkDone Hook -> kann 1 Herz füllen, wenn ihr wollt

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

    [Header("HUD timing")]
    public float chatNpcHoldSeconds = 6.0f;

    [Header("Debug")]
    public bool debugLogs = true;

    readonly List<OllamaClient.ChatMessage> _messages = new List<OllamaClient.ChatMessage>();
    DialogueAgent _currentPlayerAgent;

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

        // Default-Perspektive: Hearts aus
        ApplyHeartsVisibilityAndResetIfNeeded(reset: false);
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        if (debugLogs) Debug.Log($"[DialogueManager] OnSwitched {from?.name} -> {to?.name} | reset chat history");

        InterruptDialogue();
        RefreshCurrentPlayerAgent(resetHistory: true);
        state = DialogueState.Listening;

        if (hud != null)
            hud.SetIdlePerspective();

        // Bei jeder neuen Perspektive: Hearts reset (aber nur wenn besessen)
        ApplyHeartsVisibilityAndResetIfNeeded(reset: true);
    }

    void ApplyHeartsVisibilityAndResetIfNeeded(bool reset)
    {
        // Default ist: EnteredCount <= 1 (Start-Char). Dort keine Hearts anzeigen.
        bool isDefaultPerspective = (swapManager == null) || (swapManager.EnteredCount <= 1);

        if (HUDHearts.Instance == null) return;

        if (isDefaultPerspective)
        {
            HUDHearts.Instance.Hide();
            return;
        }

        // besessen: anzeigen
        if (reset) HUDHearts.Instance.ResetHeartsAndShow();
        else HUDHearts.Instance.ResetHeartsAndShow(); // beim Start in besessen (falls Start nicht default ist)
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
        if (_currentPlayerAgent) _currentPlayerAgent.StopSpeaking();
        state = DialogueState.Interrupted;
        if (debugLogs) Debug.Log("[DialogueManager] InterruptDialogue -> state=Interrupted");
    }

    public async System.Threading.Tasks.Task PlayerSpokeFromWav(string wavPath)
    {
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

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

        if (hud != null)
            hud.SetNpcTimed(npcText, chatNpcHoldSeconds);

        AudioClip clip = await speechOutput.TextToSpeech(npcText);
        _currentPlayerAgent.Speak(clip);

        // OPTIONAL: Wenn ihr "freier Talk" als 3. Herz zählen wollt:
        // -> Füllt 1 Herz, sobald einmal ein echter Chat durchgelaufen ist.
        // -> Entfernen, wenn ihr das lieber über Quest-Logic macht.
        if (swapManager != null && swapManager.EnteredCount > 1 && HUDHearts.Instance != null)
        {
            // nur wenn noch nicht voll (damit es nicht dauernd weiterzählt)
            if (HUDHearts.Instance.GetFilled() < 3)
                HUDHearts.Instance.Advance();
        }

        state = DialogueState.Listening;
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