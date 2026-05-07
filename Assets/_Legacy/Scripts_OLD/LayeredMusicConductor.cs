using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LayeredMusicConductor : MonoBehaviour
{
    public static LayeredMusicConductor Instance;

    public enum QuantizeMode
    {
        LoopBoundary, // neuer Layer startet am nächsten Loop-Start des Base-Layers
        BPMGrid       // neuer Layer startet am nächsten Bar-Grid (BPM muss stimmen)
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
    public bool loop = true;
    public bool startBaseLayerOnStart = true;

    [Header("Mix")]
    [Range(0f, 1f)] public float masterVolume = 1f;

    [Tooltip("Optional: Minimaler Fade-In für neue Layer.")]
    public float layerFadeIn = 0f;

    [Tooltip("Minimaler Safety-Offset beim schedulen (sek). 0.02 ist safe.")]
    public double safetyOffset = 0.02;

    [Header("Hard Sync (Phase-Lock)")]
    [Tooltip("Wenn true: alle Layer werden permanent an den Base-Layer (Layer 0) phase-locked.\n" +
             "Das verhindert Drift auch bei minimal unterschiedlichen Clip-Längen.")]
    public bool hardSyncEnabled = true;

    [Tooltip("Wie oft pro Sekunde prüfen wir Drift? 10-30 ist genug.")]
    [Range(1, 60)]
    public int resyncCheckHz = 20;

    [Tooltip("Ab wie vielen Samples Drift wird resynct? 64-512 ist ein guter Range.\n" +
             "Je niedriger, desto 'härter' gelockt.")]
    public int resyncThresholdSamples = 128;

    [Tooltip("Wenn Resync passiert: kurze Fade-Out/In Zeit gegen Clicks. 0 = kein Fade.")]
    public float resyncFadeSeconds = 0.02f;

    [Header("Debug")]
    public bool debugLogs = true;

    // intern
    readonly List<AudioSource> _activeSources = new();
    readonly Dictionary<AudioSource, float> _targetVolumes = new();

    int _nextLayerIndex = 0;
    double _referenceStartDspTime = -1.0;

    float _resyncTimer = 0f;

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
            AddNextLayerQuantized("Start");
    }

    void Update()
    {
        // Layer Fade-In (optional)
        if (layerFadeIn > 0f)
        {
            foreach (var kv in _targetVolumes)
            {
                var src = kv.Key;
                if (!src) continue;

                float target = kv.Value;
                src.volume = Mathf.MoveTowards(src.volume, target, Time.deltaTime / Mathf.Max(0.001f, layerFadeIn));
            }
        }

        // Hard Sync check (nicht jeden Frame nötig)
        if (hardSyncEnabled && _activeSources.Count >= 2 && _activeSources[0] != null)
        {
            _resyncTimer += Time.deltaTime;
            float interval = 1f / Mathf.Max(1, resyncCheckHz);

            if (_resyncTimer >= interval)
            {
                _resyncTimer = 0f;
                HardSyncToBase();
            }
        }
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        AddNextLayerQuantized($"Switch {from?.name} -> {to?.name}");
    }

    void AddNextLayerQuantized(string reason)
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
        int idx = _nextLayerIndex;
        _nextLayerIndex++;

        if (!clip)
        {
            if (debugLogs) Debug.LogWarning("[Music] Nächster Clip ist NULL.");
            return;
        }

        // AudioSource pro Layer
        var src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.clip = clip;
        src.spatialBlend = 0f; // 2D
        src.volume = (layerFadeIn > 0f) ? 0f : masterVolume;

        // Referenz setzen (Startpunkt des ganzen Layer-Systems)
        if (_referenceStartDspTime < 0.0)
            _referenceStartDspTime = AudioSettings.dspTime + safetyOffset;

        double startDsp = ComputeNextStartTime();

        // Start immer am Loop-Start (Sample 0) – dann greift HardSync später als Safety-Net
        try { src.timeSamples = 0; } catch { /* ignore */ }

        if (debugLogs)
            Debug.Log($"[Music] Add Layer {idx} '{clip.name}' | mode={quantizeMode} startDsp={startDsp:0.000} reason='{reason}'");

        src.PlayScheduled(startDsp);

        _activeSources.Add(src);
        _targetVolumes[src] = masterVolume;

        if (layerFadeIn > 0f)
            StartCoroutine(SnapTargetAfterSchedule(src, startDsp));
    }

    IEnumerator SnapTargetAfterSchedule(AudioSource src, double startDsp)
    {
        // wartet bis kurz nach Start, dann targetVolume sicher setzen (für fade)
        while (AudioSettings.dspTime < startDsp + 0.01)
            yield return null;

        if (src) _targetVolumes[src] = masterVolume;
    }

    double ComputeNextStartTime()
    {
        // wenn noch keine Source läuft -> referenceStart
        if (_activeSources.Count == 0)
            return _referenceStartDspTime;

        if (quantizeMode == QuantizeMode.LoopBoundary)
        {
            // Quantize auf nächste Loop-Startzeit des Base Layers (Layer 0)
            var baseClip = _activeSources[0].clip;
            double loopDur = ClipDurationSeconds(baseClip);

            double now = AudioSettings.dspTime + safetyOffset;
            double elapsed = now - _referenceStartDspTime;
            if (elapsed < 0) elapsed = 0;

            double loops = Math.Ceiling(elapsed / loopDur);
            return _referenceStartDspTime + loops * loopDur;
        }
        else
        {
            // Quantize auf Bar-Grid
            double beatDur = 60.0 / bpm;
            double barDur = beatsPerBar * beatDur;
            double quantum = barsPerQuantize * barDur;

            double now = AudioSettings.dspTime + safetyOffset;
            double elapsed = now - _referenceStartDspTime;
            if (elapsed < 0) elapsed = 0;

            double quanta = Math.Ceiling(elapsed / quantum);
            return _referenceStartDspTime + quanta * quantum;
        }
    }

    // =========================================================
    // HARD SYNC (Phase Lock)
    // =========================================================

    void HardSyncToBase()
    {
        var baseSrc = _activeSources[0];
        if (!baseSrc || !baseSrc.clip) return;
        if (!baseSrc.isPlaying) return;

        int baseTotal = baseSrc.clip.samples;
        if (baseTotal <= 0) return;

        // Phase 0..1 basierend auf Base timeSamples
        int basePos = SafeTimeSamples(baseSrc);
        double phase01 = (basePos % (double)baseTotal) / baseTotal;

        for (int i = 1; i < _activeSources.Count; i++)
        {
            var src = _activeSources[i];
            if (!src || !src.clip) continue;
            if (!src.isPlaying) continue;

            int total = src.clip.samples;
            if (total <= 0) continue;

            int desired = (int)Math.Round(phase01 * total) % total;
            int current = SafeTimeSamples(src);

            int delta = ShortestSampleDelta(current, desired, total);

            if (Math.Abs(delta) >= resyncThresholdSamples)
            {
                if (debugLogs)
                    Debug.Log($"[Music] RESYNC layer#{i} '{src.clip.name}' deltaSamples={delta} (thr={resyncThresholdSamples})");

                if (resyncFadeSeconds > 0f)
                    StartCoroutine(CoResyncWithFade(src, desired, resyncFadeSeconds));
                else
                    ForceSetTimeSamples(src, desired);
            }
        }
    }

    IEnumerator CoResyncWithFade(AudioSource src, int desiredSamples, float fadeSeconds)
    {
        if (!src) yield break;

        float originalTarget = _targetVolumes.TryGetValue(src, out float tv) ? tv : masterVolume;

        // quick fade out
        float t = 0f;
        float startVol = src.volume;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            if (src) src.volume = Mathf.Lerp(startVol, 0f, t / fadeSeconds);
            yield return null;
        }

        if (!src) yield break;

        ForceSetTimeSamples(src, desiredSamples);

        // fade back in to target
        t = 0f;
        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            if (src) src.volume = Mathf.Lerp(0f, originalTarget, t / fadeSeconds);
            yield return null;
        }

        if (src) src.volume = originalTarget;
    }

    int SafeTimeSamples(AudioSource src)
    {
        try { return src.timeSamples; }
        catch { return 0; }
    }

    void ForceSetTimeSamples(AudioSource src, int samples)
    {
        try { src.timeSamples = Mathf.Clamp(samples, 0, src.clip.samples - 1); }
        catch { /* ignore */ }
    }

    int ShortestSampleDelta(int current, int desired, int modulo)
    {
        // liefert Delta mit Wrap-Around, kleinster Weg
        int raw = desired - current;
        int half = modulo / 2;

        if (raw > half) raw -= modulo;
        else if (raw < -half) raw += modulo;

        return raw;
    }

    double ClipDurationSeconds(AudioClip clip)
    {
        if (!clip || clip.samples <= 0 || clip.frequency <= 0) return 0.0;
        return (double)clip.samples / clip.frequency;
    }

    // =========================================================

    [ContextMenu("Reset Music Layers")]
    public void ResetLayers()
    {
        StopAllCoroutines();

        foreach (var s in _activeSources)
        {
            if (s) Destroy(s);
        }

        _activeSources.Clear();
        _targetVolumes.Clear();

        _nextLayerIndex = 0;
        _referenceStartDspTime = -1.0;
        _resyncTimer = 0f;

        if (debugLogs) Debug.Log("[Music] Reset.");
    }
}