using System.Collections;
using UnityEngine;

public class LetterRecordTask : WorldTaskInteractable
{
    [Header("Letter Recording")]
    public string recordPrompt = "Hold the right button and speak your message";
    public string afterTextPrefix = "Message: ";

    new IEnumerator CoRunTask()
    {
        _isRunning = true;

        // 1) Prompt: bitte aufnehmen
        if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX)
            HUDText.Instance.SetSticky(recordPrompt);

        // 2) STT Request starten (SpeechInput liefert später Text)
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

        // warten bis SpeechInput liefert
        while (!gotText)
            yield return null;

        // 3) Text anzeigen
        if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX)
            HUDText.Instance.SetSticky(afterTextPrefix + transcript);

        // optional: kurze Zeit stehen lassen
        yield return new WaitForSeconds(2.5f);

        // 4) complete
        if (QuestStateManager.Instance != null && ownerCharacter != null)
            QuestStateManager.Instance.CompleteTask(ownerCharacter, taskId);

        _isCompleted = true;

        // 5) hide letter
        if (hideAfterComplete && hideTargets != null)
        {
            for (int i = 0; i < hideTargets.Length; i++)
                if (hideTargets[i] != null) hideTargets[i].SetActive(false);
        }

        if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX)
            HUDText.Instance.SetIdleAuto();

        _isRunning = false;
    }

    protected override void Update()
    {
        var swap = FindObjectOfType<PerspectiveSwapManager>();
        if (!swap || !swap.current || !swap.current.inputs) return;

        if (ownerCharacter != null && swap.current != ownerCharacter) return;

        if (!_isCompleted && QuestStateManager.Instance != null && ownerCharacter != null)
            _isCompleted = QuestStateManager.Instance.IsTaskDone(ownerCharacter, taskId);

        if (_isCompleted) return;

        float dist = Vector3.Distance(transform.position, swap.current.transform.position);

        if (dist <= interactRadius)
        {
            if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX && !HUDText.Instance.IsSticky)
                HUDText.Instance.SetPrompt(hudPrompt);

            if (swap.current.inputs.interact && !_isRunning)
            {
                if (!PrerequisitesMet(ownerCharacter))
                {
                    if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX)
                        HUDText.Instance.SetNpcTimed(lockedPrompt, 1.2f);
                    return;
                }

                StartCoroutine(CoRunTask());
            }
        }
        else
        {
            if (hidePromptWhenFar && HUDText.Instance != null && HUDText.Instance.CurrentMode == HUDText.Mode.Prompt)
                HUDText.Instance.SetIdleAuto();
        }
    }
}