using System.Collections;
using UnityEngine;

public class CharacterAmbientBed : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;

    [Header("Fade")]
    public float fadeSeconds = 1.5f;

    [Header("Base Settings")]
    [Range(0f, 1f)] public float masterVolume = 1.0f;

    AudioSource _a;
    AudioSource _b;
    AudioSource _active;
    Coroutine _fadeRoutine;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();

        // Zwei Sources für Crossfade
        _a = gameObject.AddComponent<AudioSource>();
        _b = gameObject.AddComponent<AudioSource>();

        Setup(_a);
        Setup(_b);

        _active = _a;
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
        // initialer Loop beim Start (current)
        if (swapManager != null && swapManager.current != null)
            PlayFor(swapManager.current, instant: true);
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        PlayFor(to, instant: false);
    }

    void PlayFor(PossessableCharacter character, bool instant)
    {
        if (character == null) return;

        var clip = character.ambientLoop;

        // Falls kein Clip gesetzt: einfach ausfaden
        if (clip == null)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeOutAll());
            return;
        }

        float targetVol = Mathf.Clamp01(character.ambientVolume * masterVolume);
        float targetPitch = (character.ambientPitch <= 0f) ? 1f : character.ambientPitch;

        AudioSource incoming = (_active == _a) ? _b : _a;
        AudioSource outgoing = _active;

        incoming.clip = clip;
        incoming.loop = true;
        incoming.pitch = targetPitch;
        incoming.volume = instant ? targetVol : 0f;
        incoming.Play();

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);

        if (instant)
        {
            // outgoing stoppen
            if (outgoing.isPlaying) outgoing.Stop();
            _active = incoming;
            return;
        }

        _fadeRoutine = StartCoroutine(CrossFade(outgoing, incoming, targetVol, fadeSeconds));
        _active = incoming;
    }

    IEnumerator CrossFade(AudioSource outSrc, AudioSource inSrc, float inTargetVol, float seconds)
    {
        float t = 0f;
        float outStart = outSrc != null ? outSrc.volume : 0f;

        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);

            if (inSrc) inSrc.volume = Mathf.Lerp(0f, inTargetVol, k);
            if (outSrc) outSrc.volume = Mathf.Lerp(outStart, 0f, k);

            yield return null;
        }

        if (outSrc)
        {
            outSrc.volume = 0f;
            outSrc.Stop();
            outSrc.clip = null;
        }

        if (inSrc) inSrc.volume = inTargetVol;
        _fadeRoutine = null;
    }

    IEnumerator FadeOutAll()
    {
        float t = 0f;
        float a0 = _a.volume;
        float b0 = _b.volume;

        while (t < fadeSeconds)
        {
            t += Time.deltaTime;
            float k = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(t / fadeSeconds);
            _a.volume = Mathf.Lerp(a0, 0f, k);
            _b.volume = Mathf.Lerp(b0, 0f, k);
            yield return null;
        }

        _a.Stop(); _a.clip = null; _a.volume = 0f;
        _b.Stop(); _b.clip = null; _b.volume = 0f;
        _fadeRoutine = null;
    }

    static void Setup(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f; // 2D
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
    }
}