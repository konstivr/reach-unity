using UnityEngine;

public class ShadowFollowUnreachable : MonoBehaviour
{
    public PerspectiveSwapManager swapManager;

    [Header("Distances")]
    public float minDistance = 4.0f;   // näher darf er nie werden
    public float targetDistance = 6.0f;
    public float maxDistance = 10.0f;

    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float turnSpeed = 540f;

    void Awake()
    {
        if (!swapManager) swapManager = FindObjectOfType<PerspectiveSwapManager>();
    }

    void Update()
    {
        if (!swapManager || swapManager.current == null) return;

        var p = swapManager.current.transform;
        Vector3 toShadow = transform.position - p.position;
        toShadow.y = 0f;

        float dist = toShadow.magnitude;
        if (dist < 0.001f) toShadow = Vector3.forward;

        // Wenn zu nah: weg bewegen
        Vector3 dir;
        if (dist < minDistance) dir = toShadow.normalized;                 // weg vom player
        else if (dist > maxDistance) dir = (-toShadow).normalized;         // näher ran
        else
        {
            float delta = dist - targetDistance;
            if (Mathf.Abs(delta) < 0.25f) return;
            dir = (delta > 0f) ? (-toShadow).normalized : toShadow.normalized;
        }

        // bewegen
        Vector3 next = transform.position + dir * moveSpeed * Time.deltaTime;
        transform.position = next;

        // drehen
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion tr = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, tr, turnSpeed * Time.deltaTime);
        }
    }
}