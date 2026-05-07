using UnityEngine;
using StarterAssets;

public class AutoLookFromMove : MonoBehaviour
{
    public StarterAssetsInputs inputs;
    public float yawSpeed = 220f;
    public float deadzone = 0.15f;

    void Awake()
    {
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
    }

    void Update()
    {
        if (!inputs) return;

        float x = inputs.move.x;
        if (Mathf.Abs(x) < deadzone)
        {
            inputs.LookInput(Vector2.zero);
            return;
        }

        float yawDelta = x * yawSpeed * Time.deltaTime;
        inputs.LookInput(new Vector2(yawDelta, 0f));
    }
}