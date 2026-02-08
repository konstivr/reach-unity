using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LayeredMusicConductor : MonoBehaviour
{
    public static LayeredMusicConductor Instance;

    public enum QuantizeMode
    {
        LoopBoundary, // neuer Layer startet exakt am nächsten Loop-Start
        BPMGrid       // neuer Layer startet am nächsten Bar/Beat-Grid
    }

    [Header("References")]
    public PerspectiveSwapManager swapManager;

    [Header("Layer Clips (chronologisch hinzukommend)")]
    public List<AudioClip> layers = new List<AudioClip>();

    [Header("Quantization")]
    public QuantizeMode quantizeMode = QuantizeMode.LoopBoundary;

    [Tooltip("Nur für BPMGrid: BPM des Stücks (muss stimmen).")]
    public double bpm = 120.0;

    [Tooltip("Nur für BPMGrid: z.B. 4 = 4/4.")]
    public int beatsPerBar = 4;

    [Tooltip("Nur für BPMGrid: auf wie viele Bars quantisieren? z.B. 4 Bars = neuer Layer immer am 4-Bar-Anfang.")]
    public int barsPerQuantize = 4;

    [Header("Loop / Sync")]
    [Tooltip("Wenn true: alle Layer laufen als Loop.")]
    public bool loop = true;

    [Tooltip("Startet Basis-Layer direkt beim Start (Layer 0).")]
    public bool startBaseLayerOnStart = true;

    [Header("Mix")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Tooltip("Fade-In-Zeit pro neuem Layer (sek).")]
    public float layerFadeIn = 0.35f;

    [Tooltip("Optional: beim Layer-Add ein kleines 'Click' vermeiden, indem wir minimal später starten (sek). 0.0 ist ok.")]
    public double safetyOffset = 0.02;

    [Header("Ducking (Music gets quieter while speaking)")]
    [Range(0f, 1f)] public float duckedMultiplier = 0.25f; // 25% der Musiklautstärke
    public float duckFadeDownTime = 0.12f;
    public float duckFadeUpTime = 0.25f;

    [Header("Debug")]
    public bool debugLogs = true;
    [Tooltip("Wenn true: loggt pro Frame den Duck-State (kann spammy sein).")]
    public bool debugDuckingSpam = false;

    // intern
    private readonly List<AudioSource> _activeSources = new();
    private readonly Dictionary<AudioSource, float> _baseVolumes = new(); // 0..masterVolume
    private int _nextLayerIndex = 0;

    // Referenz-Startzeitpunkt (DSP)
    private double _referenceStartDspTime = -1.0;

    // Ducking intern
    private int _duckRequests = 0;
    private float _duckFactor = 1f;      // current (smoothed)
    private float _duckTarget = 1f;      // 1 = normal, duckedMultiplier = ducked

    void Awake()
    {
        Instance = this;
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();

        if (debugLogs)
            Debug.Log($"[Music] Awake | swap={(swapManager ? "OK" : "NULL")} | layers={layers?.Count ?? 0}");
    }

    void OnEnable()
    {
        if (swapManager != null)
            swapManager.Switched += OnSwitched;
    }

    void OnDisable()
    {
        if (swapManager != null)
            swapManager.Switched -= OnSwitched;
    }

    void Start()
    {
        if (startBaseLayerOnStart)
        {
            AddNextLayerNowOrQuantized(reason: "Start");
        }
    }

    void Update()
    {
        // Duck smoothing
        float t = (_duckTarget < _duckFactor) ? duckFadeDownTime : duckFadeUpTime;
        t = Mathf.Max(0.001f, t);
        _duckFactor = Mathf.MoveTowards(_duckFactor, _duckTarget, Time.deltaTime / t);

        // Apply global duck factor to all active sources (multiplying their base-volume)
        for (int i = 0; i < _activeSources.Count; i++)
        {
            var src = _activeSources[i];
            if (!src) continue;

            float baseVol = 0f;
            _baseVolumes.TryGetValue(src, out baseVol);

            src.volume = Mathf.Clamp01(baseVol * _duckFactor);
        }

        if (debugDuckingSpam && debugLogs)
            Debug.Log($"[Music] Duck | req={_duckRequests} target={_duckTarget:0.00} factor={_duckFactor:0.00}");
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        AddNextLayerNowOrQuantized(reason: $"Switch {from?.name} -> {to?.name}");
    }

    void AddNextLayerNowOrQuantized(string reason)
    {
        if (layers == null || layers.Count == 0)
        {
            if (debugLogs) Debug.LogWarning("[Music] layers Liste ist leer.");
            return;
        }

        if (_nextLayerIndex >= layers.Count)
        {
            if (debugLogs) Debug.Log($"[Music] Keine weiteren Layer übrig. (count={layers.Count})");
            return;
        }

        var clip = layers[_nextLayerIndex];
        _nextLayerIndex++;

        if (!clip)
        {
            if (debugLogs) Debug.LogWarning("[Music] Nächster Clip ist NULL.");
            return;
        }

        // Erstelle neue AudioSource pro Layer
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.clip = clip;
        src.volume = 0f;              // wird über baseVolumes + duckFactor gefahren
        src.spatialBlend = 0f;         // 2D
        src.outputAudioMixerGroup = null;

        // Referenzzeit setzen: wenn noch nix läuft, starten wir "jetzt quantized"
        if (_referenceStartDspTime < 0.0)
        {
            _referenceStartDspTime = AudioSettings.dspTime + safetyOffset;
        }

        // Startzeitpunkt bestimmen
        double startDsp = ComputeNextStartTime();

        if (debugLogs)
        {
            Debug.Log($"[Music] Add Layer {_nextLayerIndex - 1}/{layers.Count - 1} '{clip.name}' | mode={quantizeMode} | startDsp={startDsp:0.000} | reason={reason}");
        }

        // base volume registrieren (wird gefadet)
        _baseVolumes[src] = 0f;

        // sample-genau schedulen
        src.PlayScheduled(startDsp);

        _activeSources.Add(src);

        // Fade-in: wir ändern baseVol, Update() setzt daraus src.volume * duck
        StartCoroutine(FadeInBaseVolumeRoutine(src, layerFadeIn, masterVolume));
    }

    double ComputeNextStartTime()
    {
        // Wenn noch keine Layer laufen: starte exakt an referenceStart
        if (_activeSources.Count == 0)
        {
            return _referenceStartDspTime;
        }

        // Wenn schon was läuft:
        if (quantizeMode == QuantizeMode.LoopBoundary)
        {
            var baseClip = _activeSources[0].clip;
            double loopDur = (double)baseClip.samples / baseClip.frequency;

            double now = AudioSettings.dspTime + safetyOffset;
            double elapsed = now - _referenceStartDspTime;
            if (elapsed < 0) elapsed = 0;

            double loops = System.Math.Ceiling(elapsed / loopDur);
            double next = _referenceStartDspTime + loops * loopDur;
            return next;
        }
        else
        {
            double beatDur = 60.0 / bpm;
            double barDur = beatsPerBar * beatDur;
            double quantum = barsPerQuantize * barDur;

            double now = AudioSettings.dspTime + safetyOffset;
            double elapsed = now - _referenceStartDspTime;
            if (elapsed < 0) elapsed = 0;

            double quanta = System.Math.Ceiling(elapsed / quantum);
            double next = _referenceStartDspTime + quanta * quantum;
            return next;
        }
    }

    IEnumerator FadeInBaseVolumeRoutine(AudioSource src, float seconds, float targetBaseVol)
    {
        if (!src) yield break;

        targetBaseVol = Mathf.Clamp01(targetBaseVol);

        if (seconds <= 0f)
        {
            _baseVolumes[src] = targetBaseVol;
            yield break;
        }

        float t = 0f;
        while (t < seconds && src)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / seconds);
            _baseVolumes[src] = Mathf.Lerp(0f, targetBaseVol, a);
            yield return null;
        }

        if (src) _baseVolumes[src] = targetBaseVol;
    }

    // -------------------------
    // Ducking API (call from Dialogue/Speech)
    // -------------------------
    public void RequestDuck(string reason = "")
    {
        _duckRequests++;
        _duckTarget = duckedMultiplier;

        if (debugLogs)
            Debug.Log($"[Music] Duck++ ({_duckRequests}) reason='{reason}' target={_duckTarget:0.00}");
    }

    public void ReleaseDuck(string reason = "")
    {
        _duckRequests = Mathf.Max(0, _duckRequests - 1);

        if (_duckRequests == 0)
            _duckTarget = 1f;

        if (debugLogs)
            Debug.Log($"[Music] Duck-- ({_duckRequests}) reason='{reason}' target={_duckTarget:0.00}");
    }

    public bool IsDucked => _duckRequests > 0;

    // Optional: Reset / Restart (für Debug)
    [ContextMenu("Reset Music Layers")]
    public void ResetLayers()
    {
        StopAllCoroutines();

        foreach (var s in _activeSources)
        {
            if (s) Destroy(s);
        }
        _activeSources.Clear();
        _baseVolumes.Clear();

        _nextLayerIndex = 0;
        _referenceStartDspTime = -1.0;

        _duckRequests = 0;
        _duckTarget = 1f;
        _duckFactor = 1f;

        if (debugLogs) Debug.Log("[Music] Reset.");
    }
}