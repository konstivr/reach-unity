using System;
using System.Collections.Generic;
using UnityEngine;
using Reach.Framework.Core;

namespace Reach.Framework.Audio
{
    /// <summary>
    /// Layered ambient music: each perspective switch adds the next layer,
    /// scheduled via dspTime to start at a quantized boundary so layers stay in phase.
    ///
    /// Setup:
    ///   1) Add this component to a GameObject.
    ///   2) Drag layer clips into the layers list. Layer 0 plays at start; each switch
    ///      adds the next one until the list is exhausted.
    ///   3) Make sure all clips loop seamlessly and have the same length (LoopBoundary mode)
    ///      OR set a correct BPM (BPMGrid mode).
    ///
    /// Pack note: The layers list is also exposed on StoryPack.musicLayers — when running
    /// inside a pack, you can pull layers from there instead of inspector. (Manual override
    /// kept here for flexibility.)
    /// </summary>
    public class LayeredMusicConductor : MonoBehaviour
    {
        public enum QuantizeMode
        {
            /// <summary>New layer starts at the next loop boundary of layer 0 (recommended).</summary>
            LoopBoundary,
            /// <summary>New layer starts at the next bar boundary based on bpm/beatsPerBar.</summary>
            BPMGrid
        }

        [Header("Layers")]
        [Tooltip("Override the StoryPack's music layers. Leave empty to use StoryPack.musicLayers.")]
        public List<AudioClip> layersOverride = new List<AudioClip>();

        [Header("Behaviour")]
        public bool startBaseLayerOnStart = true;

        [Header("Quantization")]
        public QuantizeMode quantizeMode = QuantizeMode.LoopBoundary;

        [Tooltip("Only for BPMGrid mode.")]
        public double bpm = 120.0;
        public int beatsPerBar = 4;
        public int barsPerQuantize = 4;

        [Header("Mix")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        public bool loop = true;

        [Tooltip("Safety offset when scheduling future starts (sec).")]
        public double safetyOffset = 0.02;

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        readonly List<AudioSource> _activeSources = new List<AudioSource>();
        int _nextLayerIndex;
        double _referenceStartDspTime = -1.0;

        IPerspectiveManager _perspective;

        // ============================================================
        // Lifecycle
        // ============================================================

        void OnEnable()
        {
            _perspective = GameContext.Instance?.Perspective;
            if (_perspective != null)
                _perspective.Switched += OnSwitched;
        }

        void OnDisable()
        {
            if (_perspective != null)
                _perspective.Switched -= OnSwitched;
            _perspective = null;
        }

        void Start()
        {
            if (startBaseLayerOnStart)
                AddNextLayer("Start");
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            AddNextLayer($"Switch {from?.name} -> {to?.name}");
        }

        // ============================================================
        // Layer adding
        // ============================================================

        List<AudioClip> ResolveLayers()
        {
            if (layersOverride != null && layersOverride.Count > 0)
                return layersOverride;

            var pack = GameContext.Instance?.pack;
            return pack != null ? pack.musicLayers : null;
        }

        void AddNextLayer(string reason)
        {
            var layers = ResolveLayers();
            if (layers == null || layers.Count == 0)
            {
                if (debugLogs) Debug.Log("[Music] No layers configured.");
                return;
            }

            if (_nextLayerIndex >= layers.Count)
            {
                if (debugLogs) Debug.Log($"[Music] All {layers.Count} layers already added.");
                return;
            }

            var clip = layers[_nextLayerIndex];
            int idx = _nextLayerIndex++;

            if (clip == null)
            {
                if (debugLogs) Debug.LogWarning($"[Music] Layer {idx} is null.");
                return;
            }

            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.clip = clip;
            src.spatialBlend = 0f; // 2D
            src.volume = masterVolume;

            // Set reference once
            if (_referenceStartDspTime < 0.0)
                _referenceStartDspTime = AudioSettings.dspTime + safetyOffset;

            double startDsp = ComputeNextStartTime();

            try { src.timeSamples = 0; } catch { }
            src.PlayScheduled(startDsp);

            _activeSources.Add(src);

            if (debugLogs)
                Debug.Log($"[Music] Layer {idx} '{clip.name}' scheduled at dsp={startDsp:0.000} (reason: {reason})");
        }

        double ComputeNextStartTime()
        {
            if (_activeSources.Count == 0)
                return _referenceStartDspTime;

            if (quantizeMode == QuantizeMode.LoopBoundary)
            {
                var baseClip = _activeSources[0].clip;
                double loopDur = ClipDurationSeconds(baseClip);
                if (loopDur <= 0) return AudioSettings.dspTime + safetyOffset;

                double now = AudioSettings.dspTime + safetyOffset;
                double elapsed = Math.Max(0, now - _referenceStartDspTime);
                double loops = Math.Ceiling(elapsed / loopDur);
                return _referenceStartDspTime + loops * loopDur;
            }
            else
            {
                double beatDur = 60.0 / bpm;
                double quantum = barsPerQuantize * beatsPerBar * beatDur;

                double now = AudioSettings.dspTime + safetyOffset;
                double elapsed = Math.Max(0, now - _referenceStartDspTime);
                double quanta = Math.Ceiling(elapsed / quantum);
                return _referenceStartDspTime + quanta * quantum;
            }
        }

        static double ClipDurationSeconds(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.frequency <= 0) return 0.0;
            return (double)clip.samples / clip.frequency;
        }

        // ============================================================
        // Manual control
        // ============================================================

        [ContextMenu("Reset Music")]
        public void Reset()
        {
            foreach (var s in _activeSources)
                if (s != null) Destroy(s);

            _activeSources.Clear();
            _nextLayerIndex = 0;
            _referenceStartDspTime = -1.0;

            if (debugLogs) Debug.Log("[Music] Reset.");
        }
    }
}