using UnityEngine;
using Cinemachine;

namespace Reach.Framework.Core
{
    /// <summary>
    /// Updates a Cinemachine Virtual Camera's Follow/LookAt to track the
    /// currently controlled character, listening to PerspectiveManager.Switched.
    ///
    /// Place this on the same GameObject as the CinemachineVirtualCamera.
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class CameraFollow : MonoBehaviour
    {
        [Header("Fallback")]
        [Tooltip("If the target character has no cameraTarget assigned, fall back to its transform.")]
        public bool fallbackToCharacterTransform = true;

        CinemachineVirtualCamera _vcam;
        IPerspectiveManager _perspective;

        void Awake()
        {
            _vcam = GetComponent<CinemachineVirtualCamera>();
        }

        void OnEnable()
        {
            // Hook into the PerspectiveManager once GameContext is initialized
            var ctx = GameContext.Instance;
            if (ctx == null)
            {
                Debug.LogWarning("[CameraFollow] No GameContext.Instance yet; will retry in Start.");
                return;
            }
            BindToPerspective(ctx.Perspective);
        }

        void Start()
        {
            // Retry if perspective wasn't ready in OnEnable
            if (_perspective == null)
            {
                var ctx = GameContext.Instance;
                if (ctx != null) BindToPerspective(ctx.Perspective);
            }

            // Apply initial state
            if (_perspective != null && _perspective.Current != null)
                Apply(_perspective.Current);
        }

        void OnDisable()
        {
            if (_perspective != null)
                _perspective.Switched -= OnSwitched;
            _perspective = null;
        }

        void BindToPerspective(IPerspectiveManager pm)
        {
            if (pm == null) return;
            if (_perspective == pm) return;

            if (_perspective != null)
                _perspective.Switched -= OnSwitched;

            _perspective = pm;
            _perspective.Switched += OnSwitched;
        }

        void OnSwitched(PossessableCharacter from, PossessableCharacter to)
        {
            Apply(to);
        }

        void Apply(PossessableCharacter c)
        {
            if (_vcam == null || c == null) return;

            Transform target = c.cameraTarget;
            if (target == null && fallbackToCharacterTransform)
                target = c.transform;

            if (target == null) return;

            _vcam.Follow = target;
            _vcam.LookAt = target;
        }
    }
}