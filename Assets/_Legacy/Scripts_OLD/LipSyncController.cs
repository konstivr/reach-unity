using UnityEngine;

[RequireComponent(typeof(Animator))]
public class LipSyncController : MonoBehaviour
{
    [Header("Animator")]
    public Animator animator;
    public string mouthOpenParam = "MouthOpen";

    [Header("Audio")]
    public AudioSource source;
    public int sampleSize = 256;

    [Header("Tuning")]
    [Range(0f, 3f)] public float gain = 8f;
    [Range(0f, 1f)] public float smooth = 0.25f;

    [Header("Debug")]
    public bool debugLogs = false;

    float[] _samples;
    float _current;
    int _mouthParamHash;
    bool _paramValid;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        _samples = new float[Mathf.Max(64, sampleSize)];

        _mouthParamHash = Animator.StringToHash(mouthOpenParam);
        _paramValid = animator && animator.parameters != null;

        // Prüfen ob Param existiert
        bool found = false;
        if (animator)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == mouthOpenParam) { found = true; break; }
            }
        }

        if (!found)
        {
            Debug.LogError($"[LipSyncController] Animator '{animator?.name}' hat keinen Float-Parameter '{mouthOpenParam}'.");
        }

        if (debugLogs)
            Debug.Log($"[LipSyncController] Awake '{name}' | animator={(animator ? "OK" : "NULL")} | source={(source ? "OK" : "NULL")} | param='{mouthOpenParam}' found={found}");
    }

    void Update()
    {
        if (!animator || !source || !source.isPlaying)
        {
            SetMouth(0f);
            return;
        }

        source.GetOutputData(_samples, 0);

        float sum = 0f;
        for (int i = 0; i < _samples.Length; i++) sum += _samples[i] * _samples[i];
        float rms = Mathf.Sqrt(sum / _samples.Length);

        float target = Mathf.Clamp01(rms * gain);
        _current = Mathf.Lerp(_current, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, smooth)));

        SetMouth(_current);
    }

    void SetMouth(float v)
    {
        if (!animator) return;
        animator.SetFloat(_mouthParamHash, v);
    }

    public void ResetMouth()
    {
        _current = 0f;
        SetMouth(0f);
    }
}