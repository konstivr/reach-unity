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

    protected override IEnumerator RunTaskRoutine(HUDText hud)
    {
        // optional click audio
        yield return PlayAudioIfAny();

        // timer relax
        yield return CoTimerRelax();

        // complete + hide
        CompleteTaskAndApply(hud);
    }

    IEnumerator CoTimerRelax()
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, durationSeconds);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
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
}