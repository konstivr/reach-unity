using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Text-to-speech using Windows SAPI via PowerShell.
    /// Generates WAV via System.Speech.Synthesis, then loads as AudioClip.
    /// </summary>
    public class WindowsSapiTTS : MonoBehaviour, ITextToSpeech
    {
        [Header("Defaults")]
        [Tooltip("Default voice when CharacterDefinition.voiceWindows is empty. " +
                 "Common: 'Microsoft Zira Desktop', 'Microsoft David Desktop', 'Microsoft Hedda Desktop' (DE).")]
        public string defaultVoice = "Microsoft Zira Desktop";

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public async Task<AudioClip> SynthesizeAsync(string text, string voiceName)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[WindowsSapiTTS] Not ready (not on Windows).");
                return null;
            }

            text = (text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;

            string voice = string.IsNullOrEmpty(voiceName) ? defaultVoice : voiceName;

            string outDir = Path.Combine(Application.persistentDataPath, "tts");
            Directory.CreateDirectory(outDir);
            string wavPath = Path.Combine(outDir, $"sapi_{DateTime.Now:HHmmssfff}.wav");

            // PowerShell script: load System.Speech, set voice, save to WAV.
            string ps = $@"
Add-Type -AssemblyName System.Speech;
$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;
try {{ $s.SelectVoice('{voice.Replace("'", "''")}'); }} catch {{ }}
$s.SetOutputToWaveFile('{wavPath.Replace("'", "''")}');
$s.Speak('{text.Replace("'", "''")}');
$s.Dispose();
";

            string args = $"-NoProfile -ExecutionPolicy Bypass -Command \"{ps.Replace("\"", "\\\"")}\"";

            if (debugLogs) Debug.Log($"[WindowsSapiTTS] powershell voice='{voice}'");

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                using var proc = new System.Diagnostics.Process { StartInfo = psi };
                proc.Start();
                _ = await proc.StandardOutput.ReadToEndAsync();
                string err = await proc.StandardError.ReadToEndAsync();
                await Task.Run(() => proc.WaitForExit());

                if (proc.ExitCode != 0 || !File.Exists(wavPath))
                {
                    Debug.LogWarning($"[WindowsSapiTTS] powershell failed exit={proc.ExitCode}\n{err}");
                    return null;
                }

                var clip = await LoadWav(wavPath);
                TryDelete(wavPath);

                if (debugLogs) Debug.Log($"[WindowsSapiTTS] OK voice='{voice}' len={(clip != null ? clip.length.ToString("0.00") : "0")}s");
                return clip;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WindowsSapiTTS] Exception: {ex}");
                return null;
            }
        }

        static async Task<AudioClip> LoadWav(string path)
        {
            string url = "file://" + path;
            using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[WindowsSapiTTS] Load failed: {req.error}");
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(req);
        }

        static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}