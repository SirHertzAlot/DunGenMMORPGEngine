using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DunGen.Startup
{
    /// <summary>
    /// Very lightweight movement/combat input loop for test worlds.
    /// - WASD: movement
    /// - Mouse X: yaw look
    /// - Space: jump
    /// - Left Mouse / F: execute combat turn
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class SimpleTestPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float jumpHeight = 1.2f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float clientActionTurnCooldown = 0.25f;

        private CharacterController _controller;
        private float _verticalVelocity;
        private float _nextCombatInputTime;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            EnsureCamera();
        }

        private void Update()
        {
            HandleMovement();
            HandleGameplayInput();
            HandleCombatInput();
        }

        private void HandleMovement()
        {
            var moveX = GetMoveX();
            var moveZ = GetMoveZ();
            var move = ResolveMoveVector(moveX, moveZ) * moveSpeed;

            var planarMove = new Vector3(move.x, 0f, move.z);
            if (planarMove.sqrMagnitude > 0.0001f)
            {
                var targetRotation = Quaternion.LookRotation(planarMove.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            if (_controller.isGrounded)
            {
                if (_verticalVelocity < 0f)
                    _verticalVelocity = -2f;

                if (IsJumpPressed())
                    _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity += gravity * Time.deltaTime;
            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);
        }

        private void HandleGameplayInput()
        {
            if (Time.time < _nextCombatInputTime)
                return;

            if (!TryGetMoveIntent(out var dx, out var dy))
                return;

            var starter = FindAnyObjectByType<SimulationStarter>();
            if (starter == null)
                return;

            if (starter.TrySubmitPlayerMove(dx, dy, "player-move-input"))
                _nextCombatInputTime = Time.time + clientActionTurnCooldown;
        }

        private void HandleCombatInput()
        {
            if (Time.time < _nextCombatInputTime)
                return;

            if (!IsCombatPressed())
                return;

            var starter = FindAnyObjectByType<SimulationStarter>();
            if (starter == null)
                return;

            if (starter.TrySubmitPlayerAttack("player-combat-input"))
            {
                _nextCombatInputTime = Time.time + clientActionTurnCooldown;
            }
        }

        private static bool TryGetMoveIntent(out int dx, out int dy)
        {
            dx = 0;
            dy = 0;
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current == null)
                return false;

            if (Keyboard.current.wKey.wasPressedThisFrame) { dy = 1; return true; }
            if (Keyboard.current.sKey.wasPressedThisFrame) { dy = -1; return true; }
            if (Keyboard.current.aKey.wasPressedThisFrame) { dx = -1; return true; }
            if (Keyboard.current.dKey.wasPressedThisFrame) { dx = 1; return true; }
            return false;
#else
            if (Input.GetKeyDown(KeyCode.W)) { dy = 1; return true; }
            if (Input.GetKeyDown(KeyCode.S)) { dy = -1; return true; }
            if (Input.GetKeyDown(KeyCode.A)) { dx = -1; return true; }
            if (Input.GetKeyDown(KeyCode.D)) { dx = 1; return true; }
            return false;
#endif
        }

        private void EnsureCamera()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("DunGen Orbital Camera");
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            var orbit = mainCamera.GetComponent<OrbitalCameraFollow>();
            if (orbit == null)
                orbit = mainCamera.gameObject.AddComponent<OrbitalCameraFollow>();

            orbit.SetTarget(transform);
        }

        private static Vector3 ResolveMoveVector(float moveX, float moveZ)
        {
            if (Camera.main != null)
            {
                var camTransform = Camera.main.transform;
                var forward = Vector3.ProjectOnPlane(camTransform.forward, Vector3.up).normalized;
                var right = Vector3.ProjectOnPlane(camTransform.right, Vector3.up).normalized;
                return (right * moveX + forward * moveZ).normalized;
            }

            return new Vector3(moveX, 0f, moveZ).normalized;
        }

        private static float GetMoveX()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current == null)
                return 0f;

            var value = 0f;
            if (Keyboard.current.aKey.isPressed) value -= 1f;
            if (Keyboard.current.dKey.isPressed) value += 1f;
            return value;
#else
        return ReadLegacyAxis("Horizontal", true);
#endif
        }

        private static float GetMoveZ()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            if (Keyboard.current == null)
                return 0f;

            var value = 0f;
            if (Keyboard.current.sKey.isPressed) value -= 1f;
            if (Keyboard.current.wKey.isPressed) value += 1f;
            return value;
#else
        return ReadLegacyAxis("Vertical", true);
#endif
        }

        private static bool IsJumpPressed()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetButtonDown("Jump");
#endif
        }

        private static bool IsCombatPressed()
        {
#if ENABLE_INPUT_SYSTEM || UNITY_INPUT_SYSTEM
            var mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            var keyboardPressed = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            return mousePressed || keyboardPressed;
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
#endif
        }

        private static float ReadLegacyAxis(string axisName, bool raw = false)
        {
            try
            {
                return raw ? Input.GetAxisRaw(axisName) : Input.GetAxis(axisName);
            }
            catch (InvalidOperationException)
            {
                // Input Manager is disabled when project uses Input System package only.
                return 0f;
            }
        }
    }
}
