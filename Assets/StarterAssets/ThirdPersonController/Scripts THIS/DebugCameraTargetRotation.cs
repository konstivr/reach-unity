using UnityEngine;
using StarterAssets;

public class DebugCameraTargetRotation : MonoBehaviour
{
    public StarterAssetsInputs inputs;
    public ThirdPersonController tpc;

    void Awake()
    {
        if (!inputs) inputs = GetComponent<StarterAssetsInputs>();
        if (!tpc) tpc = GetComponent<ThirdPersonController>();
    }

    void Update()
    {
        if (!inputs || !tpc) return;

        var tgt = tpc.CinemachineCameraTarget;
        var tgtYaw = tgt ? tgt.transform.eulerAngles.y : -1f;

        Debug.Log(
            $"[DBG] move=({inputs.move.x:0.00},{inputs.move.y:0.00}) " +
            $"look=({inputs.look.x:0.00},{inputs.look.y:0.00}) " +
            $"LockCam={tpc.LockCameraPosition} " +
            $"CamTargetYaw={tgtYaw:0.0}"
        );
    }
}