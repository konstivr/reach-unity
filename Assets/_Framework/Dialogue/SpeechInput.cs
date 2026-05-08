using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Reach.Framework.Core;
using Reach.Framework.HUD;
using Reach.Framework.Interaction;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Handles microphone recording when player presses Speak.
    /// Records to a temp WAV file and routes it to:
    ///   - GateSystem (if waiting for passphrase)
    ///   - DialogueManager (for chat — coming in later häppchen)
    ///
    /// AutoSend pattern: press once → speak → after N seconds it auto-stops and routes.
    /// No second click needed. Works around gamepad button-reading edge cases.
    /// </summary>
    public class SpeechInput : MonoBehaviour
    {
        [Header("Mic")]
        [Tooltip("Microphone device name. Empty = first available device.")]
        public string microphoneDevice = "";

        [Tooltip("Sample rate for recording.")]
        public int frequency = 16000;

        [Header("AutoSend")]
        [Tooltip("Auto-stop and route N seconds after recording started.")]
        public float autoSendSeconds = 6f;

        [Tooltip("Discard recordings shorter than this.")]
        public float minRecordSeconds = 0.25f;

        [Header("HUD Texts")]
        [TextArea(1, 3)] public string recordingPrompt = "Speak and wait...";
        [TextArea(1, 3)] public string sendingPrompt = "Sending...";
        [TextArea(1, 3)] public string canceledPrompt = "Canceled.";

        [Header("Debug")]
        public bool debugLogs = true;

        // ============================================================
        // State
        // ============================================================

        AudioClip _recording;
        bool _isRecording;
        bool _isStopping;
        float _recordStartTime;

        // ============================================================
        // Lifecycle
        // ============================================================

        void Update()
        {
            var ctx = GameContext.Instance;
            if (ctx == null) return;

            var input = ctx.Input;
            var gate = ctx.Gate;
            var hud = ctx.Hud;

            if (input == null) return;

            // Cancel always works
            if (input.CancelDown)
            {
                if (_isRecording) HardStop();
                gate?.CancelGate();
                if (hud != null && hud.IsFree)
                    hud.SetTimed(canceledPrompt, 0.8f);
                return;
            }

            // Start recording on Speak press
            if (input.SpeakDown && !_isRecording && !_isStopping)
            {
                bool gateWaiting = gate != null && gate.IsWaitingForPassphrase;
                bool gateBusy = gate != null && gate.IsGateBusy;

                // Block speak if gate is busy but not waiting
                if (gateBusy && !gateWaiting)
                {
                    if (debugLogs) Debug.Log("[SpeechInput] Blocked: gate busy (not waiting).");
                    return;
                }

                StartRecording();
            }

            // AutoSend
            if (_isRecording && !_isStopping)
            {
                float elapsed = Time.time - _recordStartTime;
                if (elapsed >= autoSendSeconds && elapsed >= minRecordSeconds)
                {
                    _ = StopAndRouteAsync();
                }
            }
        }

        void StartRecording()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[SpeechInput] No microphone devices found.");
                return;
            }

            string device = string.IsNullOrEmpty(microphoneDevice)
                ? Microphone.devices[0]
                : microphoneDevice;

            _isRecording = true;
            _isStopping = false;
            _recordStartTime = Time.time;
            microphoneDevice = device;

            // Suspend gate timeout while we record
            GameContext.Instance?.Gate?.SetTimeoutSuspended(true);

            // HUD: recording feedback
            var hud = GameContext.Instance?.Hud;
            if (hud != null && hud.IsFree)
                hud.SetSticky(recordingPrompt);

            int lenSec = Mathf.CeilToInt(autoSendSeconds + 0.5f);
            _recording = Microphone.Start(device, false, lenSec, frequency);

            if (debugLogs) Debug.Log($"[SpeechInput] START device='{device}' freq={frequency}");
        }

        async Task StopAndRouteAsync()
        {
            if (_isStopping) return;
            _isStopping = true;

            int pos = 0;
            try { pos = Microphone.GetPosition(microphoneDevice); } catch { }
            try { Microphone.End(microphoneDevice); } catch { }

            _isRecording = false;
            GameContext.Instance?.Gate?.SetTimeoutSuspended(false);

            if (_recording == null || pos <= 0)
            {
                if (debugLogs) Debug.LogWarning("[SpeechInput] Empty recording.");
                _isStopping = false;
                return;
            }

            // Save to WAV
            float[] data = new float[pos];
            _recording.GetData(data, 0);

            string dir = Path.Combine(Application.temporaryCachePath, "ReachMic");
            Directory.CreateDirectory(dir);
            string wavPath = Path.Combine(dir, $"mic_{DateTime.Now:HHmmssfff}.wav");

            try { SaveWav16Mono(wavPath, data, frequency); }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechInput] Save WAV failed: {ex}");
                _isStopping = false;
                return;
            }

            if (debugLogs) Debug.Log($"[SpeechInput] STOP -> {wavPath}");

            // Route
            var ctx = GameContext.Instance;
            var gate = ctx?.Gate;
            var hud = ctx?.Hud;

            if (gate != null && gate.IsWaitingForPassphrase)
            {
                if (debugLogs) Debug.Log("[SpeechInput] Route -> Gate passphrase");
                await gate.TryHandlePassphraseAsync(wavPath);
            }
            else if (ctx?.Dialogue != null)
            {
                if (debugLogs) Debug.Log("[SpeechInput] Route -> Chat");

                // HUD: feedback while we wait for STT/LLM/TTS
                if (hud != null && hud.IsFree)
                    hud.SetSticky(sendingPrompt);

                await ctx.Dialogue.PlayerSpokeAsync(wavPath);
            }
            else
            {
                if (debugLogs) Debug.LogWarning("[SpeechInput] No DialogueManager available.");
            }

            SafeDelete(wavPath);
            _isStopping = false;

            // Clear our sticky if still active; Router will repopulate idle/prompt next frame
            if (hud != null && hud.IsSticky)
                hud.ClearSticky();
        }

        void HardStop()
        {
            try { if (_isRecording) Microphone.End(microphoneDevice); } catch { }
            _isRecording = false;
            _isStopping = false;
            _recording = null;

            GameContext.Instance?.Gate?.SetTimeoutSuspended(false);
        }

        // ============================================================
        // WAV helpers
        // ============================================================

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
        }

        static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}