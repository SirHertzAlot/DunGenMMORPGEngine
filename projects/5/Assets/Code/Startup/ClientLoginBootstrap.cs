using UnityEngine;
using UnityEngine.SceneManagement;
using DunGen.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DunGen.Startup
{
    public enum AuthUiMode
    {
        Login,
        Register,
        ForgotUsername,
        ResetPassword
    }

    /// <summary>
    /// Presents an authoritative login, registration, password reset, and username recovery UI on Play.
    /// Ensures all players must authenticate before the world runtime and simulation launch.
    /// </summary>
    public sealed class ClientLoginBootstrap : MonoBehaviour
    {
        private AuthUiMode _currentMode = AuthUiMode.Login;

        // Login fields
        private string _username = ClientAuthState.DefaultTestUsername;
        private string _password = ClientAuthState.DefaultTestPassword;

        // Register fields
        private string _registerUsername = "";
        private string _registerEmail = "";
        private string _registerPassword = "";
        private string _registerConfirmPassword = "";

        // Forgot Username fields
        private string _forgotUsernameEmail = "";

        // Reset Password fields
        private string _resetUsernameOrEmail = "";
        private string _resetNewPassword = "";
        private string _resetConfirmPassword = "";

        // Status messages
        private string _error = "";
        private string _statusMessage = "";
        private bool _isLoading;
        private bool _isBusy;

        [SerializeField] private BackendConnectionConfig connectionConfig;
        [SerializeField] private bool loadSceneAfterLogin = true;
        [SerializeField] private string testWorldSceneName = "SampleScene";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureLoginBootstrap()
        {
            if (FindAnyObjectByType<ClientLoginBootstrap>() != null)
                return;

            var bootstrap = new GameObject("DunGen Client Login");
            DontDestroyOnLoad(bootstrap);
            bootstrap.AddComponent<ClientLoginBootstrap>();
        }

        private void Start()
        {
            if (connectionConfig == null)
                connectionConfig = Resources.Load<BackendConnectionConfig>("DunGenNetworkingConfig");

            if (ClientAuthState.IsAuthenticated)
            {
                LaunchWorld();
            }
        }

        private void OnGUI()
        {
            if (ClientAuthState.IsAuthenticated)
                return;

            var panelWidth = 420f;
            var panelHeight = _currentMode == AuthUiMode.Register ? 380f : 320f;
            var panelRect = new Rect(
                (Screen.width - panelWidth) * 0.5f,
                (Screen.height - panelHeight) * 0.5f,
                panelWidth,
                panelHeight);

            GUILayout.BeginArea(panelRect, GUI.skin.window);

            DrawHeaderTabs();

            GUILayout.Space(8);

            switch (_currentMode)
            {
                case AuthUiMode.Login:
                    DrawLoginForm();
                    break;
                case AuthUiMode.Register:
                    DrawRegisterForm();
                    break;
                case AuthUiMode.ForgotUsername:
                    DrawForgotUsernameForm();
                    break;
                case AuthUiMode.ResetPassword:
                    DrawResetPasswordForm();
                    break;
            }

            DrawStatusFeedback();

            GUILayout.EndArea();
        }

        private void DrawHeaderTabs()
        {
            GUILayout.BeginHorizontal();
            GUI.color = _currentMode == AuthUiMode.Login ? Color.cyan : Color.white;
            if (GUILayout.Button("Login")) SwitchMode(AuthUiMode.Login);

            GUI.color = _currentMode == AuthUiMode.Register ? Color.cyan : Color.white;
            if (GUILayout.Button("Register")) SwitchMode(AuthUiMode.Register);

            GUI.color = _currentMode == AuthUiMode.ForgotUsername ? Color.cyan : Color.white;
            if (GUILayout.Button("Forgot User")) SwitchMode(AuthUiMode.ForgotUsername);

            GUI.color = _currentMode == AuthUiMode.ResetPassword ? Color.cyan : Color.white;
            if (GUILayout.Button("Reset Pass")) SwitchMode(AuthUiMode.ResetPassword);

            GUI.color = Color.white;
            GUILayout.EndHorizontal();
        }

        private void SwitchMode(AuthUiMode mode)
        {
            _currentMode = mode;
            _error = "";
            _statusMessage = "";
        }

        private void DrawLoginForm()
        {
            GUILayout.Label("=== Player Login ===");
            GUILayout.Label("Username");
            _username = GUILayout.TextField(_username, 32);

            GUILayout.Label("Password");
            _password = GUILayout.PasswordField(_password, '*', 32);

            GUILayout.Space(6);
            if (GUILayout.Button("Login & Enter World") && !_isBusy)
            {
                StartCoroutine(AuthenticateWithBackend());
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Account")) SwitchMode(AuthUiMode.Register);
            if (GUILayout.Button("Forgot Username?")) SwitchMode(AuthUiMode.ForgotUsername);
            if (GUILayout.Button("Forgot Password?")) SwitchMode(AuthUiMode.ResetPassword);
            GUILayout.EndHorizontal();
        }

        private void DrawRegisterForm()
        {
            GUILayout.Label("=== Create New Account ===");
            GUILayout.Label("Username (min 3 chars)");
            _registerUsername = GUILayout.TextField(_registerUsername, 32);

            GUILayout.Label("Email Address");
            _registerEmail = GUILayout.TextField(_registerEmail, 64);

            GUILayout.Label("Password (min 4 chars)");
            _registerPassword = GUILayout.PasswordField(_registerPassword, '*', 32);

            GUILayout.Label("Confirm Password");
            _registerConfirmPassword = GUILayout.PasswordField(_registerConfirmPassword, '*', 32);

            GUILayout.Space(6);
            if (GUILayout.Button("Register & Play") && !_isBusy)
            {
                StartCoroutine(RegisterWithBackend());
            }

            if (GUILayout.Button("Back to Login")) SwitchMode(AuthUiMode.Login);
        }

        private void DrawForgotUsernameForm()
        {
            GUILayout.Label("=== Recover Username ===");
            GUILayout.Label("Registered Email Address");
            _forgotUsernameEmail = GUILayout.TextField(_forgotUsernameEmail, 64);

            GUILayout.Space(8);
            if (GUILayout.Button("Find My Username") && !_isBusy)
            {
                StartCoroutine(ForgotUsernameWithBackend());
            }

            if (GUILayout.Button("Back to Login")) SwitchMode(AuthUiMode.Login);
        }

        private void DrawResetPasswordForm()
        {
            GUILayout.Label("=== Reset Password ===");
            GUILayout.Label("Username or Email");
            _resetUsernameOrEmail = GUILayout.TextField(_resetUsernameOrEmail, 64);

            GUILayout.Label("New Password (min 4 chars)");
            _resetNewPassword = GUILayout.PasswordField(_resetNewPassword, '*', 32);

            GUILayout.Label("Confirm New Password");
            _resetConfirmPassword = GUILayout.PasswordField(_resetConfirmPassword, '*', 32);

            GUILayout.Space(6);
            if (GUILayout.Button("Update Password") && !_isBusy)
            {
                StartCoroutine(ResetPasswordWithBackend());
            }

            if (GUILayout.Button("Back to Login")) SwitchMode(AuthUiMode.Login);
        }

        private void DrawStatusFeedback()
        {
            if (_isBusy)
            {
                GUILayout.Space(4);
                GUILayout.Label("Contacting server...");
            }

            if (!string.IsNullOrWhiteSpace(_statusMessage))
            {
                GUILayout.Space(4);
                GUI.color = Color.green;
                GUILayout.Label(_statusMessage);
                GUI.color = Color.white;
            }

            if (!string.IsNullOrWhiteSpace(_error))
            {
                GUILayout.Space(4);
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label(_error);
                GUI.color = Color.white;
            }
        }

        private IEnumerator AuthenticateWithBackend()
        {
            _isBusy = true;
            _error = "";
            _statusMessage = "";

            var username = (_username ?? string.Empty).Trim();
            var password = _password ?? string.Empty;
            var completed = false;

            yield return ClientAuthenticationLayer.Authenticate(
                connectionConfig,
                username,
                password,
                dto =>
                {
                    var expires = ParseUtcOrDefault(dto.expiresAtUtc, DateTime.UtcNow.AddMinutes(30));
                    if (!ClientAuthState.SetAuthenticatedSession(dto.userId, dto.token, dto.canary, expires))
                    {
                        _error = "Backend auth returned an invalid session payload.";
                    }
                    completed = true;
                },
                error =>
                {
                    _error = error;
                    completed = true;
                });

            _isBusy = false;

            if (!completed || !ClientAuthState.IsAuthenticated)
            {
                BackendObservabilityBridge.TryEmitClientEvent(
                    "client.login.failed",
                    "client.auth",
                    string.IsNullOrWhiteSpace(username) ? "player:anonymous" : $"player:{username}",
                    "Login failed.",
                    new Dictionary<string, string> { ["username"] = username, ["reason"] = _error },
                    (uint)Time.frameCount);
                yield break;
            }

            BackendObservabilityBridge.TryEmitClientEvent(
                "client.login.succeeded",
                "client.auth",
                $"player:{ClientAuthState.AuthenticatedUsername}",
                "Login succeeded.",
                new Dictionary<string, string> { ["username"] = ClientAuthState.AuthenticatedUsername },
                (uint)Time.frameCount);

            LaunchWorld();
        }

        private IEnumerator RegisterWithBackend()
        {
            _isBusy = true;
            _error = "";
            _statusMessage = "";

            var username = (_registerUsername ?? string.Empty).Trim();
            var email = (_registerEmail ?? string.Empty).Trim();
            var password = _registerPassword ?? string.Empty;
            var confirmPassword = _registerConfirmPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
            {
                _error = "Username must be at least 3 characters.";
                _isBusy = false;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                _error = "Valid email address is required.";
                _isBusy = false;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                _error = "Password must be at least 4 characters.";
                _isBusy = false;
                yield break;
            }

            if (password != confirmPassword)
            {
                _error = "Passwords do not match.";
                _isBusy = false;
                yield break;
            }

            var completed = false;
            yield return ClientAuthenticationLayer.Register(
                connectionConfig,
                username,
                email,
                password,
                dto =>
                {
                    var expires = ParseUtcOrDefault(dto.expiresAtUtc, DateTime.UtcNow.AddMinutes(30));
                    ClientAuthState.SetAuthenticatedSession(dto.userId, dto.token, dto.canary, expires);
                    completed = true;
                },
                err =>
                {
                    _error = err;
                    completed = true;
                });

            _isBusy = false;

            if (!completed || !ClientAuthState.IsAuthenticated)
            {
                BackendObservabilityBridge.TryEmitClientEvent(
                    "client.register.failed",
                    "client.auth",
                    $"player:{username}",
                    "Registration failed.",
                    new Dictionary<string, string> { ["username"] = username, ["reason"] = _error },
                    (uint)Time.frameCount);
                yield break;
            }

            BackendObservabilityBridge.TryEmitClientEvent(
                "client.register.succeeded",
                "client.auth",
                $"player:{ClientAuthState.AuthenticatedUsername}",
                "Registration succeeded.",
                new Dictionary<string, string> { ["username"] = username, ["email"] = email },
                (uint)Time.frameCount);

            LaunchWorld();
        }

        private IEnumerator ForgotUsernameWithBackend()
        {
            _isBusy = true;
            _error = "";
            _statusMessage = "";

            var email = (_forgotUsernameEmail ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            {
                _error = "Please enter a valid email address.";
                _isBusy = false;
                yield break;
            }

            yield return ClientAuthenticationLayer.ForgotUsername(
                connectionConfig,
                email,
                dto =>
                {
                    _statusMessage = $"Success! Your username is: '{dto.username}'";
                    _username = dto.username;
                },
                err =>
                {
                    _error = err;
                });

            _isBusy = false;
        }

        private IEnumerator ResetPasswordWithBackend()
        {
            _isBusy = true;
            _error = "";
            _statusMessage = "";

            var userOrEmail = (_resetUsernameOrEmail ?? string.Empty).Trim();
            var newPass = _resetNewPassword ?? string.Empty;
            var confirmPass = _resetConfirmPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(userOrEmail))
            {
                _error = "Please enter your username or email.";
                _isBusy = false;
                yield break;
            }

            if (string.IsNullOrWhiteSpace(newPass) || newPass.Length < 4)
            {
                _error = "New password must be at least 4 characters.";
                _isBusy = false;
                yield break;
            }

            if (newPass != confirmPass)
            {
                _error = "Passwords do not match.";
                _isBusy = false;
                yield break;
            }

            yield return ClientAuthenticationLayer.ResetPassword(
                connectionConfig,
                userOrEmail,
                newPass,
                dto =>
                {
                    _statusMessage = dto.message ?? "Password reset successfully! You can now log in.";
                    _password = newPass;
                    _currentMode = AuthUiMode.Login;
                },
                err =>
                {
                    _error = err;
                });

            _isBusy = false;
        }

        private static DateTime ParseUtcOrDefault(string text, DateTime fallback)
        {
            if (DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.ToUniversalTime();

            return fallback;
        }

        private void LaunchWorld()
        {
            if (_isLoading)
                return;

            _isLoading = true;

            if (loadSceneAfterLogin && !string.IsNullOrWhiteSpace(testWorldSceneName))
            {
                try
                {
                    BackendObservabilityBridge.TryEmitClientEvent(
                        "client.world.load.begin",
                        "client.lifecycle",
                        $"player:{ClientAuthState.AuthenticatedUsername}",
                        $"Loading world scene '{testWorldSceneName.Trim()}'.",
                        new Dictionary<string, string>
                        {
                            ["targetScene"] = testWorldSceneName.Trim(),
                        },
                        (uint)Time.frameCount);

                    SceneManager.sceneLoaded += OnSceneLoaded;
                    SceneManager.LoadScene(testWorldSceneName.Trim(), LoadSceneMode.Single);
                    return;
                }
                catch (System.Exception ex)
                {
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    Debug.LogWarning($"[DunGen] Failed to load scene '{testWorldSceneName}': {ex.Message}. Continuing in current scene.");
                }
            }

            FinalizeWorldLaunch();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BackendObservabilityBridge.TryEmitClientEvent(
                "client.world.load.completed",
                "client.lifecycle",
                $"player:{ClientAuthState.AuthenticatedUsername}",
                $"World scene loaded: {scene.name}.",
                new Dictionary<string, string>
                {
                    ["scene"] = scene.name ?? string.Empty,
                },
                (uint)Time.frameCount);
            FinalizeWorldLaunch();
        }

        private void FinalizeWorldLaunch()
        {
            if (FindAnyObjectByType<SimulationStarter>() == null)
            {
                var worldBootstrap = new GameObject("DunGen Simulation Starter");
                DontDestroyOnLoad(worldBootstrap);
                worldBootstrap.AddComponent<SimulationStarter>();
            }

            TestWorldPlayerBootstrap.EnsurePlayerInCurrentScene();

            Debug.Log($"[DunGen] User '{ClientAuthState.AuthenticatedUsername}' logged in. Loading world runtime.");
            Destroy(gameObject);
        }
    }
}

