using System.Collections;
using UnityEngine;

public class ComputerTimerTask : WorldTaskInteractable
{
    [Header("Timer")]
    public float durationSeconds = 60f;

    [Header("Shadow Animation Reduce")]
    public Animator shadowAnimator;
    public string stressFloatParam = "Stress"; // optional
    public bool reduceAnimatorLayers = true;

    [Tooltip("Von LayerIndex 1..N werden Gewichte über Zeit auf 0 gefahren (Layer 0 bleibt).")]
    public int maxLayerIndexToReduce = 3;

    public AnimationCurve relaxCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    IEnumerator CoTimer()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, durationSeconds);
            float v = relaxCurve.Evaluate(Mathf.Clamp01(t)); // 1 -> 0

            if (shadowAnimator != null)
            {
                if (!string.IsNullOrEmpty(stressFloatParam))
                    shadowAnimator.SetFloat(stressFloatParam, v);

                if (reduceAnimatorLayers)
                {
                    int max = Mathf.Min(maxLayerIndexToReduce, shadowAnimator.layerCount - 1);
                    for (int li = 1; li <= max; li++)
                        shadowAnimator.SetLayerWeight(li, v);
                }
            }

            yield return null;
        }
    }

    new IEnumerator CoRunTask()
    {
        _isRunning = true;

        // optional click audio
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

        // timer relax
        yield return CoTimer();

        // complete
        if (QuestStateManager.Instance != null && ownerCharacter != null)
            QuestStateManager.Instance.CompleteTask(ownerCharacter, taskId);

        _isCompleted = true;

        // hide computer + shadow
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
        // wie bei Car: Base-Update, aber eigene CoRunTask
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