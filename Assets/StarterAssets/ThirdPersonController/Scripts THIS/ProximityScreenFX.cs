using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class ProximityScreenFX : MonoBehaviour
{
    [Header("References")]
    public PerspectiveSwapManager swapManager;

    [Tooltip("Base Volume (Wahrnehmung des aktuell kontrollierten Characters) – z.B. dein bestehendes Global Volume.")]
    public Volume baseVolume;

    [Tooltip("Proximity Volume (Aura des Targets) – zweites Global Volume mit höherer Priority (z.B. 10).")]
    public Volume proximityVolume;

    [Header("Base Transition")]
    [Tooltip("Kurzer Fade beim Wechsel, damit der Look nicht hart 'poppt'.")]
    public float baseFadeDuration = 0.12f;

    [Header("Proximity Tuning")]
    public float blendSpeed = 8f;
    public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Ambient Pulse (optional)")]
    public bool enablePulse = true;
    public float pulseSpeed = 2.2f;
    public float pulseAmount = 0.08f;

    [Header("Swap Pulse (optional)")]
    public bool enableSwapPulse = true;
    public float swapPulsePeak = 1.0f;
    public float swapPulseDuration = 0.18f;

    [Header("Debug")]
    public bool debugLogs = false;

    float _currentAuraWeight = 0f;
    float _swapPulseAdd = 0f;
    Coroutine _swapPulseRoutine;
    Coroutine _baseFadeRoutine;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();

        if (debugLogs)
        {
            Debug.Log($"[FX] Awake | swapManager={(swapManager ? swapManager.name : "NULL")} | " +
                      $"baseVolume={(baseVolume ? baseVolume.name : "NULL")} | " +
                      $"proximityVolume={(proximityVolume ? proximityVolume.name : "NULL")}");
        }

        if (baseVolume != null) baseVolume.weight = 1f;
        if (proximityVolume != null) proximityVolume.weight = 0f;

        if (baseVolume != null && proximityVolume != null && baseVolume == proximityVolume)
            Debug.LogError("[FX] baseVolume und proximityVolume sind dasselbe Volume! Du brauchst zwei getrennte Global Volumes.");
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
        // Initiale Wahrnehmung setzen
        ApplyBaseProfileFromCurrent(immediate: true);
    }

    void Update()
    {
        if (swapManager == null || baseVolume == null || proximityVolume == null) return;
        if (swapManager.current == null || !swapManager.current.IsValid) return;

        var current = swapManager.current;

        // Nearest target im Swap-Radius finden
        var target = FindNearestTarget(current, out float dist, out float radius);

        if (target != null && target.proximityAuraProfile != null)
        {
            // Aura-Profil des Targets setzen
            if (proximityVolume.profile != target.proximityAuraProfile)
                proximityVolume.profile = target.proximityAuraProfile;

            float t = Mathf.Clamp01(1f - (dist / radius));             // 0..1 je näher desto höher
            float curved = intensityCurve.Evaluate(t);

            float pulse = 0f;
            if (enablePulse)
                pulse = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmount;

            float maxW = Mathf.Clamp01(target.proximityAuraMaxWeight);
            float targetW = Mathf.Clamp01((curved + pulse + _swapPulseAdd) * maxW);

            _currentAuraWeight = Mathf.Lerp(_currentAuraWeight, targetW, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            proximityVolume.weight = _currentAuraWeight;

            if (debugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[FX] Aura ON | target='{target.name}' dist={dist:0.00} r={radius:0.00} w={_currentAuraWeight:0.00}");
        }
        else
        {
            // keine Aura / kein Target -> runterblenden
            _swapPulseAdd = Mathf.MoveTowards(_swapPulseAdd, 0f, Time.deltaTime * 3f);

            _currentAuraWeight = Mathf.Lerp(_currentAuraWeight, 0f, 1f - Mathf.Exp(-blendSpeed * Time.deltaTime));
            proximityVolume.weight = _currentAuraWeight;

            if (_currentAuraWeight <= 0.001f && proximityVolume.profile != null)
                proximityVolume.profile = null;
        }
    }

    PossessableCharacter FindNearestTarget(PossessableCharacter current, out float dist, out float radius)
    {
        dist = float.MaxValue;

        radius = swapManager.interactRadius;
        float bestDistSqr = float.MaxValue;
        PossessableCharacter best = null;

        Vector3 pos = current.transform.position;

        foreach (var p in PossessableCharacter.ValidCharacters)
        {
            if (p == null || !p.IsValid) continue;
            if (p == current) continue;

            // Radius pro Target optional überschreiben
            float r = (p.proximityAuraRadiusOverride > 0f) ? p.proximityAuraRadiusOverride : swapManager.interactRadius;
            float rSqr = r * r;

            float dSqr = (p.transform.position - pos).sqrMagnitude;
            if (dSqr <= rSqr && dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = p;
                dist = Mathf.Sqrt(dSqr);
                radius = r;
            }
        }

        return best;
    }

    void OnSwitched(PossessableCharacter from, PossessableCharacter to)
    {
        if (debugLogs)
            Debug.Log($"[FX] Switched '{from?.name}' -> '{to?.name}'");

        ApplyBaseProfileFromCurrent(immediate: false);

        if (!enableSwapPulse) return;

        if (_swapPulseRoutine != null) StopCoroutine(_swapPulseRoutine);
        _swapPulseRoutine = StartCoroutine(SwapPulseRoutine());
    }

    void ApplyBaseProfileFromCurrent(bool immediate)
    {
        if (swapManager == null || baseVolume == null) return;
        var cur = swapManager.current;
        if (cur == null) return;

        var profile = cur.possessedPerceptionProfile;
        if (profile == null)
        {
            if (debugLogs) Debug.LogWarning($"[FX] '{cur.name}' hat kein possessedPerceptionProfile gesetzt.");
            return;
        }

        if (immediate)
        {
            baseVolume.profile = profile;
            baseVolume.weight = 1f;
            if (debugLogs) Debug.Log($"[FX] BaseProfile IMMEDIATE -> '{profile.name}' (player '{cur.name}')");
            return;
        }

        if (_baseFadeRoutine != null) StopCoroutine(_baseFadeRoutine);
        _baseFadeRoutine = StartCoroutine(BaseFadeSwap(profile, cur.name));
    }

    IEnumerator BaseFadeSwap(VolumeProfile newProfile, string playerName)
    {
        // kurz rausfaden
        float t = 0f;
        float dur = Mathf.Max(0.01f, baseFadeDuration);
        while (t < dur)
        {
            t += Time.deltaTime;
            baseVolume.weight = Mathf.Lerp(1f, 0f, t / dur);
            yield return null;
        }

        baseVolume.profile = newProfile;

        // wieder reinfaden
        t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            baseVolume.weight = Mathf.Lerp(0f, 1f, t / dur);
            yield return null;
        }

        baseVolume.weight = 1f;

        if (debugLogs) Debug.Log($"[FX] BaseProfile -> '{newProfile.name}' (player '{playerName}')");
    }

    IEnumerator SwapPulseRoutine()
    {
        // hoch
        float t = 0f;
        while (t < swapPulseDuration)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / swapPulseDuration);
            _swapPulseAdd = Mathf.Lerp(0f, swapPulsePeak, x);
            yield return null;
        }

        // runter
        t = 0f;
        while (t < swapPulseDuration)
        {
            t += Time.deltaTime;
            float x = Mathf.Clamp01(t / swapPulseDuration);
            _swapPulseAdd = Mathf.Lerp(swapPulsePeak, 0f, x);
            yield return null;
        }

        _swapPulseAdd = 0f;
        _swapPulseRoutine = null;
    }
}
