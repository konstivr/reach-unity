using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Cinemachine;
using TMPro;
using System.Reflection;

public class ReachTransitionFX : MonoBehaviour
{
    public static ReachTransitionFX Instance;

    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public HUDText hud;

    [Header("PostFX")]
    public Volume transitionVolume;               // pink-ish profile
    [Range(0f, 1f)] public float transitionVolumeMax = 1f;

    [Header("Cinemachine")]
    public CinemachineVirtualCamera vcam;
    public float shakeAmplitude = 2.4f;
    public float shakeFrequency = 1.8f;

    [Header("SFX")]
    public AudioSource sfxSource;                 // 2D AudioSource empfohlen
    public AudioClip reachSfx;
    [Range(0f, 1f)] public float reachSfxVolume = 0.9f;

    [Header("UI Overlay")]
    public CanvasGroup blackFadeGroup;            // CanvasGroup auf BlackFade (Fullscreen Image)
    public CanvasGroup centerGroup;               // CanvasGroup auf CenterGroup
    public RectTransform heartTransform;          // RectTransform von Heart
    public Image heartImage;                      // Image von Heart
    public TMP_Text centerText;                   // CenterText (groß)

    [Header("Text")]
    public string reachedText = "You have reached out";
    public float bigTextScale = 1.25f;            // etwas größer

    [Header("Timing")]
    public float buildUpSeconds = 1.8f;           // pink+shake ramp
    public float blackToFullSeconds = 1.15f;      // black fade in
    public float holdFullBlackSeconds = 0.35f;    // kurz komplett schwarz
    public float heartHoldSeconds = 2.0f;         // Herz + Text bleiben sichtbar
    public float settleAfterBlinkSeconds = 0.25f; // smoother Ende

    [Header("Heartbeat (lub-dub)")]
    public float heartBaseScale = 1.0f;
    public float heartPulseStrength = 0.42f;      // wie stark skaliert (0.3–0.6)
    public float bpm = 78f;                       // Tempo
    [Tooltip("Wie kurz/knackig die Peaks sind (kleiner = spitzer).")]
    public float peakWidth = 0.08f;               // 0.05–0.12
    public float dubDelay = 0.17f;                // zweiter Schlag nach dem ersten
    public float dubStrength = 0.65f;             // zweiter Schlag etwas schwächer

    [Header("Blink After Switch (smooth)")]
    public float blinkOpenSeconds = 0.65f;        // schwarz -> offen
    public float blinkCloseSeconds = 0.22f;       // offen -> zu
    public float blinkReopenSeconds = 0.40f;      // zu -> offen
    public float finalFadeOutSeconds = 0.45f;     // final raus

    [Header("Debug")]
    public bool debugLogs = true;

    bool _isTransitioning;
    public bool IsTransitioning => _isTransitioning;

    CinemachineComponentBase _noiseComp;
    FieldInfo _ampField;
    FieldInfo _freqField;

    void Awake()
    {
        Instance = this;

        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!hud) hud = FindObjectOfType<HUDText>();
        if (!vcam) vcam = FindObjectOfType<CinemachineVirtualCamera>();
        if (!sfxSource) sfxSource = GetComponent<AudioSource>();

        CacheNoiseFields();

        if (transitionVolume) transitionVolume.weight = 0f;
        if (blackFadeGroup) blackFadeGroup.alpha = 0f;
        if (centerGroup) centerGroup.alpha = 0f;

        if (heartImage) heartImage.enabled = false;
        if (heartTransform) heartTransform.localScale = Vector3.one * heartBaseScale;

        if (centerText)
        {
            centerText.text = "";
            centerText.transform.localScale = Vector3.one;
        }

        SetShake(0f, 0f);
    }

    void CacheNoiseFields()
    {
        _noiseComp = null;
        _ampField = null;
        _freqField = null;

        if (vcam == null) return;

        _noiseComp = vcam.GetCinemachineComponent(CinemachineCore.Stage.Noise);
        if (_noiseComp == null)
        {
            if (debugLogs) Debug.LogWarning("[ReachFX] No Noise component on VCam (Stage.Noise).");
            return;
        }

        var t = _noiseComp.GetType();
        _ampField  = t.GetField("m_AmplitudeGain", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        _freqField = t.GetField("m_FrequencyGain", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (debugLogs)
            Debug.Log($"[ReachFX] NoiseComp='{t.Name}' ampField={(_ampField!=null)} freqField={(_freqField!=null)}");
    }

    public async Task<bool> PlayReachAndSwitch(PossessableCharacter target)
    {
        if (_isTransitioning) return false;
        if (swapManager == null || target == null) return false;

        _isTransitioning = true;

        // (1) SFX
        if (sfxSource && reachSfx)
            sfxSource.PlayOneShot(reachSfx, reachSfxVolume);

        // (2) Normal HUD-Text weg — nur CenterText im Overlay bleibt
        if (hud) hud.SetFXOverride(""); // leer = nix anzeigen

        // Overlay vorbereiten
        if (centerGroup) centerGroup.alpha = 1f;
        if (blackFadeGroup) blackFadeGroup.alpha = 0f;

        if (heartImage) heartImage.enabled = true;

        if (centerText)
        {
            centerText.text = reachedText;
            centerText.transform.localScale = Vector3.one * bigTextScale;
        }

        // Intensiver Aufbau: pink + shake + heartbeat + black
        await RunBuildUp();

        // kurz komplett schwarz
        await Wait(holdFullBlackSeconds);

        // Switch
        bool switched = swapManager.TrySwitchTo(target);

        // Nach Switch: smoother Blink
        await RunBlinkOpenSmooth();

        // (3) Ende smoother cleanup
        if (centerText) centerText.text = "";
        if (heartImage) heartImage.enabled = false;
        if (centerGroup) centerGroup.alpha = 0f;
        if (transitionVolume) transitionVolume.weight = 0f;

        SetShake(0f, 0f);

        await Wait(settleAfterBlinkSeconds);

        if (hud) hud.ClearFXOverride();

        _isTransitioning = false;
        return switched;
    }

    async Task RunBuildUp()
    {
        // Reset
        if (transitionVolume) transitionVolume.weight = 0f;
        SetShake(0f, 0f);

        // Ramp pink + shake + heartbeat (buildUpSeconds)
        float t = 0f;
        while (t < buildUpSeconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / buildUpSeconds);
            float eased = EaseInOutCubic(k);

            if (transitionVolume) transitionVolume.weight = Mathf.Lerp(0f, transitionVolumeMax, eased);
            SetShake(Mathf.Lerp(0f, shakeAmplitude, eased), Mathf.Lerp(0f, shakeFrequency, eased));

            PulseHeartLubDub();
            await Task.Yield();
        }

        // Black to full
        float b = 0f;
        while (b < blackToFullSeconds)
        {
            b += Time.deltaTime;
            float k = Mathf.Clamp01(b / blackToFullSeconds);
            float eased = EaseInCubic(k);

            if (blackFadeGroup) blackFadeGroup.alpha = Mathf.Lerp(0f, 1f, eased);

            PulseHeartLubDub();
            await Task.Yield();
        }

        // Heart hold (weiter pochen lassen, Text bleibt)
        float h = 0f;
        while (h < heartHoldSeconds)
        {
            h += Time.deltaTime;
            PulseHeartLubDub();
            await Task.Yield();
        }
    }

    async Task RunBlinkOpenSmooth()
    {
        // CenterGroup ausblenden (Text/Herz weg während "Aufwachen")
        if (centerGroup) centerGroup.alpha = 0f;

        // Open
        await FadeBlack(1f, 0f, blinkOpenSeconds, EaseOutCubic);
        // Blink close
        await FadeBlack(0f, 1f, blinkCloseSeconds, EaseInOutCubic);
        // Re-open
        await FadeBlack(1f, 0f, blinkReopenSeconds, EaseOutCubic);

        // final settle
        float cur = blackFadeGroup ? blackFadeGroup.alpha : 0f;
        await FadeBlack(cur, 0f, finalFadeOutSeconds, EaseOutCubic);
    }

    async Task FadeBlack(float from, float to, float seconds, System.Func<float,float> ease)
    {
        if (blackFadeGroup == null) return;

        blackFadeGroup.alpha = from;
        float t = 0f;

        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = seconds <= 0f ? 1f : Mathf.Clamp01(t / seconds);
            float e = ease != null ? ease(k) : k;

            blackFadeGroup.alpha = Mathf.Lerp(from, to, e);
            await Task.Yield();
        }

        blackFadeGroup.alpha = to;
    }

    // (4) Echtes "lub-dub" Heartbeat
    void PulseHeartLubDub()
    {
        if (heartTransform == null) return;

        // Beat phase (0..1)
        float beatsPerSecond = Mathf.Max(1f, bpm / 60f);
        float phase = Mathf.Repeat(Time.time * beatsPerSecond, 1f);

        // Zwei Peaks: lub (phase ~0) und dub (phase ~dubDelay)
        float lub = Peak(phase, 0f, peakWidth);
        float dub = Peak(phase, dubDelay, peakWidth) * dubStrength;

        float pulse = Mathf.Clamp01(lub + dub);

        float scale = heartBaseScale * (1f + pulse * heartPulseStrength);
        heartTransform.localScale = Vector3.one * scale;
    }

    // Gaussian-ish peak around center (wrap safe)
    float Peak(float x, float center, float width)
    {
        float d = Mathf.Abs(x - center);
        d = Mathf.Min(d, 1f - d); // wrap
        // sharp peak
        float w = Mathf.Max(0.0001f, width);
        float v = Mathf.Exp(-(d * d) / (2f * w * w));
        return v;
    }

    void SetShake(float amp, float freq)
    {
        if (_noiseComp == null || _ampField == null || _freqField == null) return;
        _ampField.SetValue(_noiseComp, amp);
        _freqField.SetValue(_noiseComp, freq);
    }

    static float EaseInCubic(float x) => x * x * x;
    static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    static float EaseInOutCubic(float x) =>
        x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

    static async Task Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            await Task.Yield();
        }
    }
}