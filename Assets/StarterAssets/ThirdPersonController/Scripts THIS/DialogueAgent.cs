using UnityEngine;

public class DialogueAgent : MonoBehaviour
{
    [Header("Gate (before possess)")]
    [TextArea(3, 8)]
    public string gateTtsLine = "Sag: How are you?";

    [Tooltip("Genauer String, der erkannt werden muss (Whisper) um den Switch auszulösen.")]
    public string gatePassphrase = "how are you";

    [Range(0.5f, 1f)]
    [Tooltip("Wie ähnlich muss STT-Text sein? 1 = exakt, 0.8 = recht tolerant.")]
    public float gateSimilarityThreshold = 0.82f;

    [Header("Chat (after possess)")]
    [TextArea(4, 10)]
    [Tooltip("Dieser Prompt wird als Rollen-Kontext für Ollama genutzt, sobald man diesen Player besitzt.")]
    public string chatSystemPrompt = "Du bist Player B. Antworte kurz, in Character.";

    [Header("Audio / LipSync")]
    public AudioSource voiceSource;
    public LipSyncController lipSync;

    [Header("Runtime")]
    [HideInInspector] public PossessableCharacter owner;

    [Header("Debug")]
    public bool debugLogs = true;

    void Awake()
    {
        owner = GetComponentInParent<PossessableCharacter>();
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();

        if (debugLogs)
        {
            Debug.Log($"[DialogueAgent] Awake '{name}' | owner={(owner ? owner.name : "NULL")} | voiceSource={(voiceSource ? "OK" : "NULL")} | lipSync={(lipSync ? "OK" : "NULL")}");
            Debug.Log($"[DialogueAgent] GateLineLen={(gateTtsLine?.Length ?? 0)} | Passphrase='{gatePassphrase}' | ChatPromptLen={(chatSystemPrompt?.Length ?? 0)}");
        }
    }

    public void Speak(AudioClip clip)
    {
        if (!voiceSource || !clip)
        {
            Debug.LogError($"[DialogueAgent] '{name}' Speak failed: voiceSource or clip missing.");
            return;
        }

        voiceSource.clip = clip;
        voiceSource.Play();

        if (lipSync)
        {
            lipSync.source = voiceSource;
            lipSync.enabled = true;
        }

        if (debugLogs)
            Debug.Log($"[DialogueAgent] '{name}' Speak() -> '{clip.name}' len={clip.length:0.00}s");
    }

    public void StopSpeaking()
    {
        if (debugLogs) Debug.Log($"[DialogueAgent] '{name}' StopSpeaking()");
        if (voiceSource) voiceSource.Stop();
        if (lipSync) lipSync.ResetMouth();
    }
}