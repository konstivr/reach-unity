using UnityEngine;
using StarterAssets;

[DisallowMultipleComponent]
public class FirstPersonSteerAdapter : MonoBehaviour
{
    [Header("Refs (auto)")]
    public StarterAssetsInputs inputs;

    [Header("Steering")]
    [Tooltip("Grad pro Sekunde bei vollem Stick-Ausschlag")]
    public float turnSpeed = 220f;

    [Tooltip("Deadzone für Stick X")]
    [Range(0f, 0.5f)]
    public float turnDeadzone = 0.08f;

    [Tooltip("Wenn true: Look wird genullt, damit Kamera nicht separat driftet.")]
    public bool forceLookZero = true;

    void Awake()
    {
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
    }

    void Update()
    {
        if (!inputs) return;

        // 1) RAW Move lesen (kommt vom Joystick)
        Vector2 raw = inputs.move;

        // 2) Drehen über X (Yaw)
        float x = Mathf.Abs(raw.x) < turnDeadzone ? 0f : raw.x;
        if (x != 0f)
        {
            transform.Rotate(0f, x * turnSpeed * Time.deltaTime, 0f, Space.Self);
        }

        // 3) ThirdPersonController "nur forward/back" geben (kein Strafe)
        //    -> Kamera dreht mit, weil Player yaw sich ändert
        inputs.MoveInput(new Vector2(0f, raw.y));

        // 4) Look killen (wenn du wirklich kein extra Umsehen willst)
        if (forceLookZero)
            inputs.LookInput(Vector2.zero);
    }
}