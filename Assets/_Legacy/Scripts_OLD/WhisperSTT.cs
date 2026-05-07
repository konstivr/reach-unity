using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Alias gegen Debug-Ambiguity:
using UDebug = UnityEngine.Debug;

public class WhisperSTT : MonoBehaviour
{
    public static WhisperSTT Instance;

    [Header("Paths")]
    [Tooltip("Homebrew whisper.cpp CLI: /opt/homebrew/opt/whisper-cpp/bin/whisper-cli")]
    public string whisperBinaryPath = "/opt/homebrew/opt/whisper-cpp/bin/whisper-cli";

    [Tooltip("z.B. /Users/kvrinsum/whisper-models/ggml-small.bin")]
    public string modelPath = "";

    [Header("Language")]
    public string language = "de";

    [Header("Performance")]
    [Tooltip("Threads für whisper-cli (mehr = schneller, bis CPU voll).")]
    public int threads = 6;

    [Header("Debug")]
    public bool debugLogs = true;

    private void Awake()
    {
        Instance = this;

        if (debugLogs)
        {
            UDebug.Log($"[WhisperSTT] Awake | binary='{whisperBinaryPath}' exists={File.Exists(whisperBinaryPath)}");
            UDebug.Log($"[WhisperSTT] Awake | model='{modelPath}' exists={File.Exists(modelPath)}");
        }
    }

    public async Task<string> TranscribeWav(string wavPath)
    {
        if (string.IsNullOrWhiteSpace(whisperBinaryPath) || !File.Exists(whisperBinaryPath))
        {
            UDebug.LogError($"[WhisperSTT] whisperBinaryPath invalid: '{whisperBinaryPath}'");
            return "";
        }
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            UDebug.LogError($"[WhisperSTT] modelPath invalid: '{modelPath}'");
            return "";
        }
        if (string.IsNullOrWhiteSpace(wavPath) || !File.Exists(wavPath))
        {
            UDebug.LogError($"[WhisperSTT] wav file not found: '{wavPath}'");
            return "";
        }

        // whisper-cli args:
        // -m model -f file --language de --no-timestamps --threads N
        string args =
            $"-m \"{modelPath}\" -f \"{wavPath}\" --language {language} --no-timestamps --threads {Mathf.Max(1, threads)}";

        if (debugLogs)
            UDebug.Log($"[WhisperSTT] RUN: \"{whisperBinaryPath}\" {args}");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = whisperBinaryPath,
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
            using var proc = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true };

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await Task.Run(() => proc.WaitForExit());

            if (proc.ExitCode != 0)
            {
                UDebug.LogError($"[WhisperSTT] ExitCode={proc.ExitCode}\nSTDERR:\n{stderr}");
                return "";
            }

            string raw = stdout.ToString();
            string outText = ExtractTranscript(raw);

            if (debugLogs)
            {
                UDebug.Log($"[WhisperSTT] RAW STDOUT (first 500 chars):\n{Trunc(raw, 500)}");
                if (stderr.Length > 0) UDebug.Log($"[WhisperSTT] STDERR:\n{stderr}");
                UDebug.Log($"[WhisperSTT] TRANSCRIPT: '{outText}'");
            }

            return outText;
        }
        catch (Exception ex)
        {
            UDebug.LogError($"[WhisperSTT] Exception: {ex}");
            return "";
        }
    }

    private static string ExtractTranscript(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var lines = raw.Split('\n');
        var sb = new StringBuilder();

        foreach (var l in lines)
        {
            var line = l.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Filter typische Info-Zeilen
            if (line.StartsWith("whisper_", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("main:", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("[", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("system_info", StringComparison.OrdinalIgnoreCase)) continue;

            sb.Append(line).Append(' ');
        }

        return sb.ToString().Trim();
    }

    private static string Trunc(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}