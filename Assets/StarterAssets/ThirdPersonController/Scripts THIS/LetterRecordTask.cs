using System.Collections;
using UnityEngine;

public class LetterRecordTask : WorldTaskInteractable
{
    [Header("Letter Recording")]
    public string recordPrompt = "Hold the right button and speak your message";
    public string afterTextPrefix = "Message: ";

    [Tooltip("Wie lange der erkannte Text sichtbar bleibt, bevor abgeschlossen wird.")]
    public float showTranscriptSeconds = 2.5f;

    protected override IEnumerator RunTaskRoutine(HUDText hud)
    {
        if (hud == null) hud = HUDText.Instance;

        // 1) Prompt
        if (hud != null && !hud.IsLockedByFX)
            hud.SetSticky(recordPrompt);

        // 2) STT request (SpeechInput liefert später Text)
        bool gotText = false;
        string transcript = "";

        if (SttCaptureRouter.Instance != null)
        {
            SttCaptureRouter.Instance.Request((t) =>
            {
                transcript = t ?? "";
                gotText = true;
            });
        }
        else
        {
            // fallback: kein router vorhanden
            transcript = "";
            gotText = true;
        }

        while (!gotText)
            yield return null;

        // 3) Text anzeigen
        if (hud != null && !hud.IsLockedByFX)
            hud.SetSticky(afterTextPrefix + transcript);

        yield return new WaitForSeconds(showTranscriptSeconds);

        // 4) Complete + Hide
        CompleteTaskAndApply(hud);
    }
}