using System.Collections;
using UnityEngine;

public class CarDriveAwayTask : WorldTaskInteractable
{
    [Header("Drive Away")]
    public Transform carRoot;                 // falls nicht gesetzt: this.transform
    public Vector3 driveWorldOffset = new Vector3(0, 0, 20f);
    public float driveDuration = 2.2f;
    public AnimationCurve driveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    protected override void Awake()
    {
        base.Awake();
        if (!carRoot) carRoot = transform;
    }

    IEnumerator DriveAway()
    {
        Vector3 start = carRoot.position;
        Vector3 end = start + driveWorldOffset;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, driveDuration);
            float e = driveEase.Evaluate(Mathf.Clamp01(t));
            carRoot.position = Vector3.Lerp(start, end, e);
            yield return null;
        }

        carRoot.position = end;
    }

    // Wir überschreiben den Ablauf, damit: Audio -> Drive -> Hide -> Complete
    new IEnumerator CoRunTask()
    {
        _isRunning = true;

        // Audio
        if (audioClip != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
            }
            audioSource.clip = audioClip;
            audioSource.Play();

            if (waitForAudioToFinish)
                yield return new WaitForSeconds(audioClip.length);
        }

        // Drive
        yield return DriveAway();

        // complete
        if (QuestStateManager.Instance != null && ownerCharacter != null)
            QuestStateManager.Instance.CompleteTask(ownerCharacter, taskId);

        _isCompleted = true;

        // hide car+shadow
        if (hideAfterComplete && hideTargets != null)
        {
            for (int i = 0; i < hideTargets.Length; i++)
                if (hideTargets[i] != null) hideTargets[i].SetActive(false);
        }

        if (HUDText.Instance != null && !HUDText.Instance.IsLockedByFX && !HUDText.Instance.IsSticky)
            HUDText.Instance.SetIdleAuto();

        _isRunning = false;
    }

    protected override void Update()
    {
        // wir benutzen die Update-Logik aus der Base – aber starten unsere eigene Coroutine
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