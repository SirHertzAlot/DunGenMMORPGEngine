using UnityEngine;
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DunGen.Startup
{
    /// <summary>
    /// Simple third-person orbital camera around a target character.
    /// Right mouse drag orbits. Mouse wheel zooms.
    /// </summary>
    public sealed class OrbitalCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);
        [SerializeField] private float distance = 6f;
        [SerializeField] private float minDistance = 2f;
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private float orbitSensitivity = 140f;
        [SerializeField] private float zoomSpeed = 4f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private LayerMask cameraCollisionMask = ~0;

        private float _yaw;
        private float _pitch = 20f;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
            if (target != null)
                _yaw = target.eulerAngles.y;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            UpdateOrbitInput();
            UpdateZoomInput();
            UpdateCameraPose();
        }

        private void UpdateOrbitInput()
        {
            if (!IsOrbitHeld())
                return;

            var lookX = ReadLookX();
            var lookY = ReadLookY();
            _yaw += lookX * orbitSensitivity * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch - (lookY * orbitSensitivity * Time.deltaTime), minPitch, maxPitch);
        }

        private void UpdateZoomInput()
        {
            var scroll = ReadScroll();
            if (Mathf.Abs(scroll) < 0.0001f)
                return;

            distance = Mathf.Clamp(distance - (scroll * zoomSpeed), minDistance, maxDistance);
        }

        private void UpdateCameraPose()
        {
            var pivot = target.position + pivotOffset;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var desiredPosition = pivot - (rotation * Vector3.forward * distance);

            var finalPosition = desiredPosition;
            var direction = desiredPosition - pivot;
            var directionLength = direction.magnitude;
            if (directionLength > 0.001f)
            {
                var ray = new Ray(pivot, direction / directionLength);
                if (Physics.SphereCast(ray, 0.2f, out var hit, directionLength, cameraCollisionMask, QueryTriggerInteraction.Ignore))
                    finalPosition = hit.point + hit.normal * 0.15f;
            }

            transform.SetPositionAndRotation(finalPosition, rotation);
            transform.LookAt(pivot);
        }

        private static bool IsOrbitHeld()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
            return Input.GetMouseButton(1);
#endif
        }

        private static float ReadLookX()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().x : 0f;
#else
            return Input.GetAxis("Mouse X") * 20f;
#endif
        }

        private static float ReadLookY()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().y : 0f;
#else
            return Input.GetAxis("Mouse Y") * 20f;
#endif
        }

        private static float ReadScroll()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y * 0.01f : 0f;
#else
            return Input.mouseScrollDelta.y;
#endif
        }
    }
}