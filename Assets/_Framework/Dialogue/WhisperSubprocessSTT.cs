using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Reach.Framework.Dialogue
{
    /// <summary>
    /// Speech-to-text via the whisper.cpp 'whisper-cli' subprocess.
    /// Works on macOS and Windows — just point binaryPath / modelPath at the right files.
    ///
    /// Defaults:
    ///   macOS Homebrew: /opt/homebrew/opt/whisper-cpp/bin/whisper-cli
    ///   Windows: download whisper.cpp build, set absolute path
    /// </summary>
    public class WhisperSubprocessSTT : MonoBehaviour, ISpeechToText
    {
        [Header("Paths")]
        [Tooltip("Full path to the whisper-cli executable.")]
        public string binaryPathMac = "/opt/homebrew/opt/whisper-cpp/bin/whisper-cli";

        [Tooltip("Full path to the whisper-cli executable on Windows.")]
        public string binaryPathWindows = "C:\\whisper.cpp\\whisper-cli.exe";

        [Tooltip("Full path to the GGML model file (e.g. ggml-small.bin).")]
        public string modelPath = "";

        [Header("Performance")]
        [Range(1, 16)]
        public int threads = 6;

        [Header("Debug")]
        public bool debugLogs = true;

        public bool IsReady
        {
            get
            {
                string bin = ResolveBinaryPath();
                return File.Exists(bin) && File.Exists(modelPath);
            }
        }

        public async Task<string> TranscribeAsync(string wavPath, string language)
        {
            string bin = ResolveBinaryPath();

            if (!File.Exists(bin))
            {
                Debug.LogError($"[WhisperSTT] Binary not found: '{bin}'");
                return "";
            }
            if (!File.Exists(modelPath))
            {
                Debug.LogError($"[WhisperSTT] Model not found: '{modelPath}'");
                return "";
            }
            if (!File.Exists(wavPath))
            {
                Debug.LogError($"[WhisperSTT] Wav not found: '{wavPath}'");
                return "";
            }

            string lang = string.IsNullOrEmpty(language) ? "en" : language;

            // whisper-cli flags: -m model -f wav --language X --no-timestamps --threads N
            string args =
                $"-m \"{modelPath}\" -f \"{wavPath}\" --language {lang} --no-timestamps --threads {Mathf.Max(1, threads)}";

            if (debugLogs) Debug.Log($"[WhisperSTT] RUN: {bin} {args}");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bin,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            try
            {
                using var proc = new System.Diagnostics.Process
                {
                    StartInfo = psi,
                    EnableRaisingEvents = true
                };

                proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                await Task.Run(() => proc.WaitForExit());

                if (proc.ExitCode != 0)
                {
                    Debug.LogError($"[WhisperSTT] ExitCode={proc.ExitCode}\nSTDERR:\n{stderr}");
                    return "";
                }

                string transcript = ExtractTranscript(stdout.ToString());

                if (debugLogs)
                    Debug.Log($"[WhisperSTT] -> '{transcript}'");

                return transcript;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WhisperSTT] Exception: {ex}");
                return "";
            }
        }

        string ResolveBinaryPath()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return binaryPathMac;
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return binaryPathWindows;
#else
            return binaryPathMac;
#endif
        }

        // whisper-cli prints info lines + the transcript. Strip the info lines.
        static string ExtractTranscript(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var sb = new StringBuilder();
            var lines = raw.Split('\n');
            foreach (var l in lines)
            {
                string line = l.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                if (line.StartsWith("whisper_", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("main:", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("system_info", StringComparison.OrdinalIgnoreCase)) continue;
                if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase)) continue;

                sb.Append(line).Append(' ');
            }

            return sb.ToString().Trim();
        }
    }
}