using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

// Alias gegen Debug-Ambiguity:
using UDebug = UnityEngine.Debug;

public class SpeechOutput : MonoBehaviour
{
    public static SpeechOutput Instance;

    [Header("Preferred: Pyper (optional)")]
    [Tooltip("Falls pyper installiert: Pfad z.B. /opt/homebrew/bin/pyper oder leer lassen und usePyper=false")]
    public bool usePyper = false;
    public string pyperBinaryPath = "/opt/homebrew/bin/pyper";

    [Tooltip("Optional voice name, je nach pyper build")]
    public string pyperVoice = ""; // wird automatisch auf Englisch gesetzt, falls leer

    [Header("Fallback: macOS say")]
    [Tooltip("macOS say ist i.d.R. verfügbar")]
    public bool useMacSayFallback = true;

    [Tooltip("Wird automatisch auf Englisch gesetzt (z.B. Samantha).")]
    public string macSayVoice = "Samantha";

    [Header("Force English")]
    [Tooltip("Wenn true: erzwingt Englisch für Pyper + macOS say (Voice/Language).")]
    public bool forceEnglish = true;

    [Tooltip("Pyper Voice für Englisch (je nach Build z.B. en_US, en-US, en_GB).")]
    public string forcedPyperEnglishVoice = "en_US";

    [Tooltip("macOS say Voice für Englisch (z.B. Samantha, Alex, Daniel).")]
    public string forcedMacEnglishVoice = "Samantha";

    [Header("Audio Settings")]
    public int sampleRate = 44100;

    [Header("Debug")]
    public bool debugLogs = true;

    private void Awake()
    {
        Instance = this;

        if (debugLogs)
        {
            UDebug.Log($"[SpeechOutput] Awake | usePyper={usePyper} pyperExists={File.Exists(pyperBinaryPath)} forceEnglish={forceEnglish}");
        }

        ApplyEnglishVoicesIfNeeded();
    }

    void ApplyEnglishVoicesIfNeeded()
    {
        if (!forceEnglish) return;

        // macOS say: harte englische Stimme
        if (!string.IsNullOrWhiteSpace(forcedMacEnglishVoice))
            macSayVoice = forcedMacEnglishVoice;

        // Pyper: wenn leer oder irgendwas anderes -> auf en setzen
        if (string.IsNullOrWhiteSpace(pyperVoice))
            pyperVoice = forcedPyperEnglishVoice;
    }

    public async Task<AudioClip> TextToSpeech(string text)
    {
        text = (text ?? "").Trim();

        ApplyEnglishVoicesIfNeeded();

        if (debugLogs)
            UDebug.Log($"[SpeechOutput] TextToSpeech called | len={text.Length}");

        if (string.IsNullOrWhiteSpace(text))
        {
            UDebug.LogWarning("[SpeechOutput] TTS text empty -> returning beep");
            return CreateBeep("TTS_Beep_Empty", 0.35f, 440f);
        }

        // 1) Pyper
        if (usePyper)
        {
            var clip = await TryPyper(text);
            if (clip != null) return clip;
        }

        // 2) macOS say
        if (useMacSayFallback)
        {
            var clip = await TryMacSay(text);
            if (clip != null) return clip;
        }

        // 3) last resort
        UDebug.LogWarning("[SpeechOutput] No TTS backend available -> beep");
        return CreateBeep("TTS_Beep", 0.55f, 440f);
    }

    async Task<AudioClip> TryPyper(string text)
    {
        if (string.IsNullOrWhiteSpace(pyperBinaryPath) || !File.Exists(pyperBinaryPath))
        {
            UDebug.LogWarning($"[SpeechOutput] Pyper enabled but binary missing: '{pyperBinaryPath}'");
            return null;
        }

        string outDir = Path.Combine(Application.persistentDataPath, "tts");
        Directory.CreateDirectory(outDir);
        string wavPath = Path.Combine(outDir, $"pyper_{DateTime.Now:HHmmssfff}.wav");

        // Minimal: pyper input -> wav output
        string args = $"-o \"{wavPath}\"";

        // Erzwinge Voice (Englisch), wenn gewünscht
        string voiceToUse = pyperVoice;
        if (forceEnglish && !string.IsNullOrWhiteSpace(forcedPyperEnglishVoice))
            voiceToUse = forcedPyperEnglishVoice;

        if (!string.IsNullOrWhiteSpace(voiceToUse))
            args = $"-v {voiceToUse} " + args;

        if (debugLogs) UDebug.Log($"[SpeechOutput] Pyper RUN: \"{pyperBinaryPath}\" {args}");

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = pyperBinaryPath,
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            await proc.StandardInput.WriteAsync(text);
            proc.StandardInput.Close();

            string err = await proc.StandardError.ReadToEndAsync();
            await Task.Run(() => proc.WaitForExit());

            if (proc.ExitCode != 0 || !File.Exists(wavPath))
            {
                UDebug.LogWarning($"[SpeechOutput] Pyper failed ExitCode={proc.ExitCode}\n{err}");
                return null;
            }

            if (debugLogs) UDebug.Log($"[SpeechOutput] Pyper OK -> {wavPath}");

            return await LoadWavAsClip(wavPath, "TTS_Pyper");
        }
        catch (Exception ex)
        {
            UDebug.LogWarning($"[SpeechOutput] Pyper exception: {ex}");
            return null;
        }
    }

    async Task<AudioClip> TryMacSay(string text)
    {
        // say -v Samantha -o out.aiff "text"
        // dann: afconvert out.aiff out.wav

        string outDir = Path.Combine(Application.persistentDataPath, "tts");
        Directory.CreateDirectory(outDir);

        string aiffPath = Path.Combine(outDir, $"say_{DateTime.Now:HHmmssfff}.aiff");
        string wavPath = Path.Combine(outDir, $"say_{DateTime.Now:HHmmssfff}.wav");

        string voiceToUse = macSayVoice;
        if (forceEnglish && !string.IsNullOrWhiteSpace(forcedMacEnglishVoice))
            voiceToUse = forcedMacEnglishVoice;

        string sayArgs = $"-v \"{voiceToUse}\" -o \"{aiffPath}\" \"{EscapeQuotes(text)}\"";

        if (debugLogs) UDebug.Log($"[SpeechOutput] macOS say RUN: say {sayArgs}");

        try
        {
            // 1) say -> aiff
            int exitSay = await RunProcess("say", sayArgs);
            if (exitSay != 0 || !File.Exists(aiffPath))
            {
                UDebug.LogWarning($"[SpeechOutput] say failed exit={exitSay}");
                return null;
            }

            // 2) afconvert aiff -> wav
            string afArgs = $"\"{aiffPath}\" -o \"{wavPath}\" -f WAVE -d LEI16@{sampleRate}";
            if (debugLogs) UDebug.Log($"[SpeechOutput] afconvert RUN: afconvert {afArgs}");

            int exitAf = await RunProcess("afconvert", afArgs);
            if (exitAf != 0 || !File.Exists(wavPath))
            {
                UDebug.LogWarning($"[SpeechOutput] afconvert failed exit={exitAf}");
                return null;
            }

            if (debugLogs) UDebug.Log($"[SpeechOutput] macOS say OK -> {wavPath}");

            return await LoadWavAsClip(wavPath, "TTS_Say");
        }
        catch (Exception ex)
        {
            UDebug.LogWarning($"[SpeechOutput] macOS say exception: {ex}");
            return null;
        }
    }

    static async Task<int> RunProcess(string fileName, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new System.Diagnostics.Process { StartInfo = psi };
        proc.Start();

        _ = await proc.StandardOutput.ReadToEndAsync();
        _ = await proc.StandardError.ReadToEndAsync();

        await Task.Run(() => proc.WaitForExit());
        return proc.ExitCode;
    }

    static string EscapeQuotes(string s) => s.Replace("\"", "\\\"");

    static AudioClip CreateBeep(string name, float durSec, float freq)
    {
        int sr = 44100;
        int samples = Mathf.CeilToInt(sr * durSec);
        var clip = AudioClip.Create(name, samples, 1, sr, false);

        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / sr) * 0.2f;

        clip.SetData(data, 0);
        return clip;
    }

    static async Task<AudioClip> LoadWavAsClip(string wavPath, string clipName)
    {
        string url = "file://" + wavPath;

        using var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
        var op = req.SendWebRequest();
        while (!op.isDone) await Task.Yield();

        if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            UDebug.LogWarning($"[SpeechOutput] LoadWav failed: {req.error} ({wavPath})");
            return null;
        }

        var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
        clip.name = clipName;
        return clip;
    }
}