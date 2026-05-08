using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Text-to-speech using macOS 'say' command.
    /// say -v {voice} -o file.aiff "text"  →  afconvert to wav  →  load as AudioClip
    /// </summary>
    public class MacSayTTS : MonoBehaviour, ITextToSpeech
    {
        [Header("Defaults")]
        [Tooltip("Default voice when CharacterDefinition.voiceMacOS is empty.")]
        public string defaultVoice = "Samantha";

        [Header("Audio")]
        public int sampleRate = 44100;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady
        {
            get
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return File.Exists("/usr/bin/say");
#else
                return false;
#endif
            }
        }

        public async Task<AudioClip> SynthesizeAsync(string text, string voiceName)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[MacSayTTS] Not ready (not on macOS or 'say' missing).");
                return null;
            }

            text = (text ?? "").Trim();
            if (string.IsNullOrEmpty(text)) return null;

            string voice = string.IsNullOrEmpty(voiceName) ? defaultVoice : voiceName;

            string outDir = Path.Combine(Application.persistentDataPath, "tts");
            Directory.CreateDirectory(outDir);

            string aiffPath = Path.Combine(outDir, $"say_{DateTime.Now:HHmmssfff}.aiff");
            string wavPath  = Path.Combine(outDir, $"say_{DateTime.Now:HHmmssfff}.wav");

            try
            {
                // 1) say → aiff
                string sayArgs = $"-v \"{voice}\" -o \"{aiffPath}\" \"{Escape(text)}\"";
                if (debugLogs) Debug.Log($"[MacSayTTS] say {sayArgs}");

                int sayExit = await RunProcess("say", sayArgs);
                if (sayExit != 0 || !File.Exists(aiffPath))
                {
                    Debug.LogWarning($"[MacSayTTS] say failed exit={sayExit}");
                    return null;
                }

                // 2) afconvert aiff → wav
                string afArgs = $"\"{aiffPath}\" -o \"{wavPath}\" -f WAVE -d LEI16@{sampleRate}";
                int afExit = await RunProcess("afconvert", afArgs);
                if (afExit != 0 || !File.Exists(wavPath))
                {
                    Debug.LogWarning($"[MacSayTTS] afconvert failed exit={afExit}");
                    return null;
                }

                // 3) Load wav
                var clip = await LoadWav(wavPath);
                if (debugLogs) Debug.Log($"[MacSayTTS] OK voice='{voice}' len={(clip != null ? clip.length.ToString("0.00") : "0")}s");

                // Cleanup temp files
                TryDelete(aiffPath);
                TryDelete(wavPath);

                return clip;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MacSayTTS] Exception: {ex}");
                return null;
            }
        }

        static async Task<int> RunProcess(string fileName, string args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();
            _ = await proc.StandardOutput.ReadToEndAsync();
            _ = await proc.StandardError.ReadToEndAsync();
            await Task.Run(() => proc.WaitForExit());
            return proc.ExitCode;
        }

        static async Task<AudioClip> LoadWav(string path)
        {
            string url = "file://" + path;
            using var req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[MacSayTTS] Load failed: {req.error}");
                return null;
            }
            return DownloadHandlerAudioClip.GetContent(req);
        }

        static string Escape(string s) => s.Replace("\"", "\\\"");
        static void TryDelete(string p) { try { if (File.Exists(p)) File.Delete(p); } catch { } }
    }
}