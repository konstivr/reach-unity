using System;
using UnityEngine;

public class SttCaptureRouter : MonoBehaviour
{
    public static SttCaptureRouter Instance;

    public bool HasActiveRequest => _onTranscript != null;
    Action<string> _onTranscript;

    void Awake()
    {
        Instance = this;
    }

    public void Request(Action<string> onTranscript)
    {
        _onTranscript = onTranscript;
    }

    public void Clear()
    {
        _onTranscript = null;
    }

    public void Deliver(string text)
    {
        var cb = _onTranscript;
        _onTranscript = null;
        cb?.Invoke(text);
    }
}