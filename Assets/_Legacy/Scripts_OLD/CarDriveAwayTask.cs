using System.Collections;
using UnityEngine;

public class CarDriveAwayTask : WorldTaskInteractable
{
    [Header("Drive Away")]
    [Tooltip("Root Transform des Autos, das wegfahren soll. Wenn leer: dieses Objekt.")]
    public Transform carRoot;

    [Tooltip("Welt-Offset, in den das Auto fährt (z.B. Z+20).")]
    public Vector3 driveWorldOffset = new Vector3(0, 0, 20f);

    [Tooltip("Dauer der Fahrt in Sekunden.")]
    public float driveDuration = 2.2f;

    [Tooltip("Easing der Fahrt (0..1).")]
    public AnimationCurve driveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Detection Fix (optional but recommended)")]
    [Tooltip("Wenn true: setzt die Position dieses Task-Objekts in Awake auf carRoot.position.\n" +
             "Hilft, wenn der Distanz-Check in WorldTaskInteractable transform.position nutzt, aber das Script-Objekt nicht am Auto hängt.")]
    public bool syncTaskObjectToCarRoot = true;

    [Tooltip("Wenn > 0: warnt, wenn Task-Objekt und carRoot zu weit auseinander sind (typischer Setup-Fehler).")]
    public float warnIfTaskFartherThan = 1.5f;

    protected override void Awake()
    {
        base.Awake();

        if (!carRoot) carRoot = transform;

        // Wenn der Task-Component auf einem Child liegt, aber carRoot woanders ist,
        // misst die Base evtl. Distanz zum falschen Punkt -> Prompt kommt nie.
        float d = Vector3.Distance(transform.position, carRoot.position);

        if (warnIfTaskFartherThan > 0f && d > warnIfTaskFartherThan)
        {
            Debug.LogWarning(
                $"[CarDriveAwayTask] Setup Warning: Task-Object '{name}' ist {d:0.00}m von carRoot '{carRoot.name}' entfernt.\n" +
                $"Wenn WorldTaskInteractable transform.position für den Range-Check nutzt, wirst du beim Auto NICHT 'in range' sein.\n" +
                $"Fix: Component auf das Auto-Root legen ODER syncTaskObjectToCarRoot aktivieren."
            );
        }

        if (syncTaskObjectToCarRoot && carRoot != null)
        {
            transform.position = carRoot.position;
        }
    }

    protected override IEnumerator RunTaskRoutine(HUDText hud)
    {
        // 1) Audio (optional)
        yield return PlayAudioIfAny();

        // 2) Drive away
        yield return DriveAway();

        // 3) Complete + Hide (+ Colliders etc. in deiner Base)
        CompleteTaskAndApply(hud);
    }

    IEnumerator DriveAway()
    {
        if (!carRoot) yield break;

        Vector3 start = carRoot.position;
        Vector3 end = start + driveWorldOffset;

        float t = 0f;
        float dur = Mathf.Max(0.01f, driveDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = driveEase != null ? driveEase.Evaluate(Mathf.Clamp01(t)) : Mathf.Clamp01(t);
            carRoot.position = Vector3.Lerp(start, end, e);
            yield return null;
        }

        carRoot.position = end;
    }
}