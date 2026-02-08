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

    [Header("Mic")]
    public string microphoneDevice = null; // null = default
    public int frequency = 16000;
    public int maxSeconds = 8;

    [Header("Debug")]
    public bool debugLogs = true;

    AudioClip _recording;
    bool _isRecording;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
        if (!gate) gate = FindObjectOfType<InteractionGateProximity>();
        if (!dialogueManager) dialogueManager = FindObjectOfType<DialogueManager>();
    }

    void Update()
    {
        // Während der Reach-Transition: keine Aufnahme starten/stoppen
        if (ReachTransitionFX.Instance != null && ReachTransitionFX.Instance.IsTransitioning)
            return;

        if (!swapManager || !swapManager.current || !swapManager.current.inputs) return;

        var inputs = swapManager.current.inputs;

        // Hold-to-record (Right Button / Enter o.ä.)
        if (inputs.dialogueConfirmDown && !_isRecording)
            StartRecording();

        if (inputs.dialogueConfirmUp && _isRecording)
            _ = StopRecordingAndRoute();
    }

    void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[SpeechInput] No microphone devices found.");
            return;
        }

        _isRecording = true;
        _recording = Microphone.Start(microphoneDevice, false, maxSeconds, frequency);

        if (debugLogs)
            Debug.Log($"[SpeechInput] Recording START device='{microphoneDevice}' freq={frequency}");
    }

    async Task StopRecordingAndRoute()
    {
        _isRecording = false;

        int pos = Microphone.GetPosition(microphoneDevice);
        Microphone.End(microphoneDevice);

        if (_recording == null)
        {
            Debug.LogError("[SpeechInput] recording is NULL on stop.");
            return;
        }

        if (pos <= 0) pos = _recording.samples;

        float[] data = new float[pos];
        _recording.GetData(data, 0);

        // Save WAV (built-in)
        string dir = Path.Combine(Application.persistentDataPath, "recordings");
        Directory.CreateDirectory(dir);

        string wavPath = Path.Combine(dir, $"mic_{DateTime.Now:HHmmssfff}.wav");
        try
        {
            SaveWav16Mono(wavPath, data, frequency);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SpeechInput] SaveWav failed: {ex}");
            return;
        }

        if (debugLogs)
            Debug.Log($"[SpeechInput] Recording STOP -> saved wav: {wavPath}");

        // 1) Gate routing (Passphrase check -> Switch)
        if (gate != null && gate.IsInGateZone)
        {
            if (debugLogs) Debug.Log("[SpeechInput] Routing -> GATE passphrase");
            bool handled = await gate.TryHandleGatePassphrase(wavPath);
            if (handled) return;
        }

        // 2) Chat block (near next player OR waiting for passphrase)
        if (gate != null && gate.ShouldBlockChat())
        {
            if (debugLogs) Debug.Log("[SpeechInput] Chat blocked (gate context).");
            return;
        }

        // 3) Normal chat
        if (dialogueManager == null)
        {
            Debug.LogError("[SpeechInput] DialogueManager missing.");
            return;
        }

        if (debugLogs) Debug.Log("[SpeechInput] Routing -> CHAT (Ollama)");
        await dialogueManager.PlayerSpokeFromWav(wavPath);
    }

    // ------------------------------------------------------------
    // Minimal WAV writer: 16-bit PCM, mono
    // ------------------------------------------------------------
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
        bw.Write((short)1); // PCM
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