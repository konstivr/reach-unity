using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class SpeechInput : MonoBehaviour
{
    [Header("Refs")]
    public PerspectiveSwapManager swapManager;
    public InteractionGateProximity gate;
    public DialogueManager dialogueManager;
    public HUDText hud;

    [Header("Mic")]
    public string microphoneDevice = null;
    public int frequency = 16000;

    [Header("AutoSend")]
    public bool autoSendEnabled = true;
    public float autoSendSeconds = 10f;
    public float minRecordSeconds = 0.25f;

    [Header("HUD Texts (SpeechInput-owned)")]
    [TextArea(1, 3)] public string recordingPrompt = "Speak and wait";
    [TextArea(1, 3)] public string sendingPrompt = "Sending message...";
    [TextArea(1, 3)] public string notSwitchedYetPrompt = "Reach out first.";
    [TextArea(1, 3)] public string canceledPrompt = "Canceled.";

    [Header("Debug")]
    public bool debugLogs = true;

    AudioClip _recording;
    bool _isRecording;
    bool _isStopping;
    float _recordStartTime;
    bool _startedThisFrame;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!gate) gate = FindObjectOfType<InteractionGateProximity>();
        if (!dialogueManager) dialogueManager = FindObjectOfType<DialogueManager>();
        if (!hud) hud = FindObjectOfType<HUDText>();
    }

    void Update()
    {
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;

        var inputs = swapManager.current.inputs;
        _startedThisFrame = false;

        bool gateWaiting = gate != null && gate.IsWaitingForPassphrase;
        bool gateBusy = gate != null && gate.IsGateBusy;
        bool gateHasTarget = gate != null && gate.HasGateTargetInRange;

        // -------------------------
        // Cancel (always works)
        // -------------------------
        if (inputs.dialogueCancel)
        {
            if (debugLogs) Debug.Log("[SpeechInput] Cancel pressed -> reset + cancel gate");
            HardResetFlags();
            if (gate != null) gate.CancelGate();

            // Do NOT try to restore idle prompt here (Router owns that).
            // Just show a tiny timed feedback if possible.
            if (hud != null && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
                hud.SetNpcTimed(canceledPrompt, 0.8f);

            return;
        }

        // -------------------------
        // Start speak (DialogueConfirmDown)
        // -------------------------
        if (inputs.dialogueConfirmDown && !_isStopping && !_isRecording)
        {
            // Gate rule:
            // - If gate is WAITING for passphrase => allow start.
            // - If gate is BUSY (tts etc.) but NOT waiting => block.
            if (gateBusy && !gateWaiting)
            {
                if (debugLogs) Debug.Log("[SpeechInput] Block start: gate busy (TTS) and not waiting.");
                return;
            }

            // If not yet switched (enteredcount <=1) and also not in a gate context, block chat speak.
            // BUT we still allow recording when gateWaiting (passphrase).
            if (!gateWaiting && (swapManager == null || swapManager.EnteredCount <= 1))
            {
                if (hud != null && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
                    hud.SetNpcTimed(notSwitchedYetPrompt, 1.2f);
                return;
            }

            // If the player is not in any gate context and also no dialogueManager, block early
            if (!gateWaiting && dialogueManager == null)
            {
                Debug.LogError("[SpeechInput] DialogueManager missing.");
                return;
            }

            StartRecording();
        }

        // -------------------------
        // AutoSend (no second click)
        // -------------------------
        if (_isRecording && autoSendEnabled && !_isStopping)
        {
            float t = Time.time - _recordStartTime;
            if (t >= autoSendSeconds && t >= minRecordSeconds)
            {
                if (debugLogs) Debug.Log("[SpeechInput] AutoSend timeout reached -> stop + route.");
                _ = StopRecordingAndRoute();
            }
        }

        // -------------------------
        // Gate-zone safety: if gate context but left range while recording -> discard
        // -------------------------
        if (_isRecording && gate != null)
        {
            bool gateContext = gateBusy; // includes waiting/tts playing
            bool stillNear = gate.HasGateTargetInRange;

            if (gateContext && !stillNear && !_isStopping)
            {
                if (debugLogs) Debug.Log("[SpeechInput] Left gate range while recording -> discard + cancel gate.");
                _ = StopRecordingAndMaybeDiscard(discard: true, cancelGate: true);
            }
        }
    }

    void StartRecording()
    {
        if (_isRecording || _isStopping) return;

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[SpeechInput] No microphone devices found.");
            return;
        }

        if (string.IsNullOrEmpty(microphoneDevice))
        {
            microphoneDevice = Microphone.devices[0];
            if (debugLogs) Debug.Log($"[SpeechInput] Auto-picked microphoneDevice='{microphoneDevice}'");
        }

        _isRecording = true;
        _isStopping = false;
        _recordStartTime = Time.time;
        _startedThisFrame = true;

        if (gate != null) gate.SetTimeoutSuspended(true);

        // Recording is SpeechInput-owned => we can intentionally override Sticky
        if (hud != null && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
            hud.SetSticky(recordingPrompt);

        int lenSec = Mathf.CeilToInt(Mathf.Max(1f, autoSendSeconds + 0.5f));
        _recording = Microphone.Start(microphoneDevice, false, lenSec, frequency);

        if (debugLogs)
            Debug.Log($"[SpeechInput] Recording START device='{microphoneDevice}' freq={frequency} frame={Time.frameCount}");
    }

    async Task StopRecordingAndRoute()
    {
        await StopRecordingAndMaybeDiscard(discard: false, cancelGate: false);
    }

    async Task StopRecordingAndMaybeDiscard(bool discard, bool cancelGate)
    {
        if (_isStopping) return;
        _isStopping = true;

        if (_startedThisFrame)
            await Task.Yield();

        int pos = 0;
        try { pos = Microphone.GetPosition(microphoneDevice); }
        catch { pos = 0; }

        try { Microphone.End(microphoneDevice); } catch { /* ignore */ }

        _isRecording = false;

        if (gate != null) gate.SetTimeoutSuspended(false);

        if (_recording == null)
        {
            if (debugLogs) Debug.LogWarning("[SpeechInput] recording was NULL on stop -> reset flags");
            _isStopping = false;
            return;
        }

        if (pos <= 0) pos = _recording.samples;
        if (pos <= 0)
        {
            if (debugLogs) Debug.LogWarning("[SpeechInput] pos<=0 -> nothing recorded -> reset flags");
            _isStopping = false;
            return;
        }

        float[] data = new float[pos];
        _recording.GetData(data, 0);

        string dir = Path.Combine(Application.temporaryCachePath, Application.productName);
        Directory.CreateDirectory(dir);
        string wavPath = Path.Combine(dir, $"mic_tmp_{DateTime.Now:HHmmssfff}.wav");

        try
        {
            SaveWav16Mono(wavPath, data, frequency);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpeechInput] SaveWav failed: {ex}");
            _isStopping = false;
            return;
        }

        if (debugLogs)
            Debug.Log($"[SpeechInput] Recording STOP -> temp wav: {wavPath}");

        // Cancel gate hard?
        if (cancelGate && gate != null)
        {
            gate.CancelGate();
            // Router will restore idle prompt; clear our sticky safely
            if (hud != null && hud.IsSticky && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
                hud.ClearSticky();

            SafeDelete(wavPath);
            _isStopping = false;
            return;
        }

        // 1) Gate waiting: ALWAYS route passphrase first
        if (!discard && gate != null && gate.IsWaitingForPassphrase)
        {
            if (debugLogs) Debug.Log("[SpeechInput] Routing -> GATE passphrase (waiting)");

            bool handled = await gate.TryHandleGatePassphrase(wavPath);
            SafeDelete(wavPath);

            // allow immediate retry
            _isStopping = false;

            // Gate will set its own sticky (NoMatch / AfterGateSpoken) and/or reset.
            return;
        }

        // 2) Gate busy blocks chat (safety)
        if (!discard && gate != null && gate.IsGateBusy)
        {
            if (debugLogs) Debug.Log("[SpeechInput] Chat blocked (gate busy).");
            SafeDelete(wavPath);
            _isStopping = false;
            return;
        }

        // 3) discard
        if (discard)
        {
            SafeDelete(wavPath);
            _isStopping = false;

            // clear our recording sticky; Router restores idle
            if (hud != null && hud.IsSticky && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
                hud.ClearSticky();

            return;
        }

        // 4) Normal chat
        if (dialogueManager == null)
        {
            Debug.LogError("[SpeechInput] DialogueManager missing.");
            SafeDelete(wavPath);
            _isStopping = false;
            return;
        }

        // show sending (SpeechInput-owned)
        if (hud != null && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
            hud.SetSticky(sendingPrompt);

        if (debugLogs) Debug.Log("[SpeechInput] Routing -> CHAT (Ollama)");
        await dialogueManager.PlayerSpokeFromWav(wavPath);

        SafeDelete(wavPath);
        _isStopping = false;

        // after sending: clear our sticky; Router restores idle/prompt next frame
        if (hud != null && hud.IsSticky && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
            hud.ClearSticky();
    }

    void HardResetFlags()
    {
        try { if (_isRecording) Microphone.End(microphoneDevice); } catch { /* ignore */ }

        _isRecording = false;
        _isStopping = false;
        _recording = null;

        if (gate != null) gate.SetTimeoutSuspended(false);

        // Clear our sticky if any (Router handles idle)
        if (hud != null && hud.IsSticky && !hud.IsLockedByFX && !hud.IsTimedLocked && !hud.IsIntroRunning)
            hud.ClearSticky();
    }

    static void SafeDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }

    static void SaveWav16Mono(string path, float[] samples, int sampleRate)
    {
        int sampleCount = samples.Length;
        byte[] pcm = new byte[sampleCount * 2];

        for (int i = 0; i < sampleCount; i++)
        {
            float s = Mathf.Clamp(samples[i], -1f, 1f);
            short v = (short)Mathf.RoundToInt(s * 32767f);
            pcm[i * 2 + 0] = (byte)(v & 0xFF);
            pcm[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        int channels = 1;
        int bitsPerSample = 16;
        int byteRate = sampleRate * channels * (bitsPerSample / 8);
        short blockAlign = (short)(channels * (bitsPerSample / 8));
        int subchunk2Size = pcm.Length;
        int chunkSize = 36 + subchunk2Size;

        bw.Write(Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(Encoding.ASCII.GetBytes("WAVE"));

        bw.Write(Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write((short)bitsPerSample);

        bw.Write(Encoding.ASCII.GetBytes("data"));
        bw.Write(subchunk2Size);
        bw.Write(pcm);
        bw.Flush();
    }
}