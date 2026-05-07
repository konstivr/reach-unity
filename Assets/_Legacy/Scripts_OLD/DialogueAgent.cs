using System;
using System.Text;
using UnityEngine;

public class DialogueAgent : MonoBehaviour
{
    [Header("Gate (before possess)")]
    [TextArea(3, 8)]
    public string gateTtsLine = "Say: How are you?";

    public string gatePassphrase = "how are you";

    [Range(0.5f, 1f)]
    public float gateSimilarityThreshold = 0.82f;

    [Header("Chat (after possess)")]
    [TextArea(4, 10)]
    public string chatSystemPrompt = "Du bist Player B. Antworte kurz, in Character.";

    [Header("Audio / LipSync")]
    public AudioSource voiceSource;
    public LipSyncController lipSync;

    [Header("Runtime")]
    [HideInInspector] public PossessableCharacter owner;

    [Header("Debug")]
    public bool debugLogs = true;

    [Tooltip("Wenn true: erkennt Time-Resets/Loop/Clip-Wechsel und macht dann einen Scene-Audio Scan + Warning.")]
    public bool deepDebug = true;

    [Tooltip("Wie oft max. pro Sekunde darf er bei Anomalien scannen/loggen? (0.5 = alle 2s)")]
    [Range(0.1f, 5f)]
    public float anomalyLogCooldown = 0.75f;

    // ========= Intern =========
    int _speakCallCount = 0;
    double _lastSpeakTime = -999;
    string _lastClipName = "";
    int _lastClipSamples = -1;

    // Audio runtime tracking
    AudioClip _prevClip;
    bool _prevIsPlaying;
    bool _prevLoop;
    float _prevTime;

    float _lastAnomalyLogTime = -999f;

    void Awake()
    {
        owner = GetComponentInParent<PossessableCharacter>();
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();

        if (debugLogs)
        {
            Debug.Log(
                $"[DialogueAgent] Awake '{name}' (id={GetInstanceID()}) | owner={(owner ? owner.name : "NULL")} | " +
                $"voiceSource={(voiceSource ? $"{voiceSource.name}(id={voiceSource.GetInstanceID()})" : "NULL")} | " +
                $"lipSync={(lipSync ? "OK" : "NULL")}"
            );

            Debug.Log(
                $"[DialogueAgent] GateLineLen={(gateTtsLine?.Length ?? 0)} | Passphrase='{gatePassphrase}' | " +
                $"thr={gateSimilarityThreshold:0.00} | ChatPromptLen={(chatSystemPrompt?.Length ?? 0)}"
            );

            if (voiceSource)
            {
                Debug.Log(
                    $"[DialogueAgent] AudioSource settings | loop={voiceSource.loop} playOnAwake={voiceSource.playOnAwake} " +
                    $"spatialBlend={voiceSource.spatialBlend:0.00} volume={voiceSource.volume:0.00} pitch={voiceSource.pitch:0.00}"
                );
            }
        }

        _prevClip = voiceSource ? voiceSource.clip : null;
        _prevIsPlaying = voiceSource ? voiceSource.isPlaying : false;
        _prevLoop = voiceSource ? voiceSource.loop : false;
        _prevTime = 0f; // wichtig: NICHT voiceSource.time lesen, falls clip null
    }

    void Update()
    {
        if (!debugLogs || !voiceSource) return;

        // 0) Wenn kein Clip anliegt, dürfen wir voiceSource.time NICHT anfassen,
        // sonst kommt die Unity-Warnung ("resource that is not a clip").
        bool hasClip = (voiceSource.clip != null);

        // 1) Track modifications to loop flag
        if (voiceSource.loop != _prevLoop)
        {
            Debug.LogWarning(
                $"[DialogueAgent] LOOP FLAG CHANGED on '{name}' | {_prevLoop} -> {voiceSource.loop} " +
                $"frame={Time.frameCount} (Someone is modifying the AudioSource!)"
            );
            _prevLoop = voiceSource.loop;
        }

        // 2) Track clip changes
        if (voiceSource.clip != _prevClip)
        {
            Debug.LogWarning(
                $"[DialogueAgent] CLIP CHANGED on '{name}' | " +
                $"{(_prevClip ? _prevClip.name : "null")} -> {(voiceSource.clip ? voiceSource.clip.name : "null")} " +
                $"frame={Time.frameCount} isPlaying={voiceSource.isPlaying}"
            );
            _prevClip = voiceSource.clip;
        }

        // 3) Detect time “jump backwards” while still playing (restart/loop)
        // Nur wenn wir wirklich einen Clip haben!
        if (deepDebug && hasClip && voiceSource.isPlaying && _prevIsPlaying)
        {
            float cur = voiceSource.time;  // SAFE: clip != null
            float prev = _prevTime;

            if (cur + 0.02f < prev)
            {
                if (Time.time - _lastAnomalyLogTime > anomalyLogCooldown)
                {
                    _lastAnomalyLogTime = Time.time;

                    Debug.LogWarning(
                        $"[DialogueAgent] TIME RESET DETECTED on '{name}' | clip={voiceSource.clip.name} " +
                        $"prevTime={prev:0.00} -> curTime={cur:0.00} / len={voiceSource.clip.length:0.00} " +
                        $"loop={voiceSource.loop} frame={Time.frameCount}\n" +
                        $"This usually means: LOOP enabled OR clip restarted externally OR another AudioSource plays the same clip."
                    );

                    LogWhoPlaysSameClip(voiceSource.clip);
                }
            }

            _prevTime = cur;
        }
        else
        {
            // wenn kein Clip: wir halten _prevTime einfach auf 0
            _prevTime = hasClip ? voiceSource.time : 0f;
        }

        _prevIsPlaying = voiceSource.isPlaying;

        // 4) Lightweight periodic status (nur mit Clip)
        if (hasClip && Time.frameCount % 120 == 0 && voiceSource.isPlaying)
        {
            Debug.Log(
                $"[DialogueAgent] Playing '{name}' | clip={voiceSource.clip.name} " +
                $"time={voiceSource.time:0.00}/{voiceSource.clip.length:0.00} loop={voiceSource.loop} frame={Time.frameCount}"
            );
        }
    }

    public void Speak(AudioClip clip)
    {
        _speakCallCount++;

        if (!voiceSource || !clip)
        {
            Debug.LogError($"[DialogueAgent] '{name}' Speak failed: voiceSource or clip missing.");
            return;
        }

        double now = AudioSettings.dspTime;
        double dt = now - _lastSpeakTime;

        string clipName = clip.name;
        int clipSamples = clip.samples;

        if (debugLogs)
        {
            Debug.Log(
                $"[DialogueAgent] Speak() CALL #{_speakCallCount} on '{name}' " +
                $"frame={Time.frameCount} dt={dt:0.000}s | incomingClip='{clipName}' " +
                $"len={clip.length:0.00}s samples={clipSamples} freq={clip.frequency} ch={clip.channels}"
            );

            if (_lastClipName == clipName && _lastClipSamples == clipSamples && dt < 0.6)
            {
                Debug.LogWarning(
                    $"[DialogueAgent] SPEAK RE-TRIGGER SUSPECT: same clip quickly again (dt={dt:0.000}s)\n" +
                    $"STACK:\n{Environment.StackTrace}"
                );
            }
        }

        // Safety: stop & disable loop
        voiceSource.Stop();
        voiceSource.loop = false;

        voiceSource.clip = clip;
        voiceSource.Play();

        if (lipSync)
        {
            lipSync.source = voiceSource;
            lipSync.enabled = true;
        }

        _lastSpeakTime = now;
        _lastClipName = clipName;
        _lastClipSamples = clipSamples;

        // update trackers
        _prevClip = voiceSource.clip;
        _prevIsPlaying = voiceSource.isPlaying;
        _prevLoop = voiceSource.loop;
        _prevTime = 0f; // nicht direkt voiceSource.time lesen (safe)
    }

    public void StopSpeaking()
    {
        if (debugLogs)
        {
            Debug.Log(
                $"[DialogueAgent] StopSpeaking() '{name}' frame={Time.frameCount} " +
                $"isPlaying={(voiceSource ? voiceSource.isPlaying : false)} " +
                $"clip={(voiceSource && voiceSource.clip ? voiceSource.clip.name : "null")}"
            );
        }

        if (voiceSource) voiceSource.Stop();
        if (lipSync) lipSync.ResetMouth();
    }

    // ============================================================
    // Detect "who plays the same clip"
    // ============================================================
    void LogWhoPlaysSameClip(AudioClip clip)
    {
        if (clip == null) return;

        var all = FindObjectsOfType<AudioSource>(true);
        int matches = 0;

        var sb = new StringBuilder(512);
        sb.AppendLine($"[DialogueAgent] Audio scan: who is playing clip '{clip.name}'? totalSources={all.Length}");

        for (int i = 0; i < all.Length; i++)
        {
            var a = all[i];
            if (!a) continue;

            if (a.clip == clip)
            {
                matches++;
                sb.AppendLine(
                    $"- src='{a.name}' go='{a.gameObject.name}' active={a.gameObject.activeInHierarchy} " +
                    $"isPlaying={a.isPlaying} loop={a.loop} " +
                    $"spatialBlend={a.spatialBlend:0.00} vol={a.volume:0.00}"
                );
            }

            if (a.gameObject.name.ToLower().Contains("one shot"))
            {
                sb.AppendLine(
                    $"! OneShotCandidate: src='{a.name}' go='{a.gameObject.name}' isPlaying={a.isPlaying} " +
                    $"clip={(a.clip ? a.clip.name : "null")} loop={a.loop}"
                );
            }
        }

        sb.AppendLine($"Matches with same assigned clip: {matches}");
        Debug.LogWarning(sb.ToString());
    }
}