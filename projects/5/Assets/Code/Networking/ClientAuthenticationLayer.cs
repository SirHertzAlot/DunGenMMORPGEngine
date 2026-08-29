using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DunGen.Networking
{
    public static class ClientAuthenticationLayer
    {
        public static IEnumerator Authenticate(
            BackendConnectionConfig config,
            string username,
            string password,
            Action<UnityAuthLoginResponseDto> onSuccess,
            Action<string> onError)
        {
            if (config == null)
            {
                onError?.Invoke("Missing BackendConnectionConfig.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new UnityAuthLoginRequestDto
            {
                username = username ?? string.Empty,
                password = password ?? string.Empty
            });

            var bodyBytes = Encoding.UTF8.GetBytes(payload);
            using (var request = new UnityWebRequest(config.BuildAuthLoginUrl(), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = config.RequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var err = TryExtractErrorMessage(request.downloadHandler?.text, request.error);
                    onError?.Invoke(err);
                    yield break;
                }

                var responseText = request.downloadHandler.text ?? string.Empty;
                var dto = JsonUtility.FromJson<UnityAuthLoginResponseDto>(responseText);
                if (dto == null || string.IsNullOrWhiteSpace(dto.token) || string.IsNullOrWhiteSpace(dto.canary))
                {
                    onError?.Invoke("Auth response missing token/canary.");
                    yield break;
                }

                onSuccess?.Invoke(dto);
            }
        }

        public static IEnumerator Register(
            BackendConnectionConfig config,
            string username,
            string email,
            string password,
            Action<UnityAuthRegisterResponseDto> onSuccess,
            Action<string> onError)
        {
            if (config == null)
            {
                onError?.Invoke("Missing BackendConnectionConfig.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new UnityAuthRegisterRequestDto
            {
                username = username ?? string.Empty,
                email = email ?? string.Empty,
                password = password ?? string.Empty
            });

            var bodyBytes = Encoding.UTF8.GetBytes(payload);
            using (var request = new UnityWebRequest(config.BuildAuthRegisterUrl(), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = config.RequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var err = TryExtractErrorMessage(request.downloadHandler?.text, request.error);
                    onError?.Invoke(err);
                    yield break;
                }

                var responseText = request.downloadHandler.text ?? string.Empty;
                var dto = JsonUtility.FromJson<UnityAuthRegisterResponseDto>(responseText);
                if (dto == null || string.IsNullOrWhiteSpace(dto.token))
                {
                    onError?.Invoke("Registration failed: Invalid response from server.");
                    yield break;
                }

                onSuccess?.Invoke(dto);
            }
        }

        public static IEnumerator ForgotUsername(
            BackendConnectionConfig config,
            string email,
            Action<UnityAuthForgotUsernameResponseDto> onSuccess,
            Action<string> onError)
        {
            if (config == null)
            {
                onError?.Invoke("Missing BackendConnectionConfig.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new UnityAuthForgotUsernameRequestDto
            {
                email = email ?? string.Empty
            });

            var bodyBytes = Encoding.UTF8.GetBytes(payload);
            using (var request = new UnityWebRequest(config.BuildAuthForgotUsernameUrl(), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = config.RequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var err = TryExtractErrorMessage(request.downloadHandler?.text, request.error);
                    onError?.Invoke(err);
                    yield break;
                }

                var responseText = request.downloadHandler.text ?? string.Empty;
                var dto = JsonUtility.FromJson<UnityAuthForgotUsernameResponseDto>(responseText);
                if (dto == null)
                {
                    onError?.Invoke("Username recovery failed: Invalid response from server.");
                    yield break;
                }

                onSuccess?.Invoke(dto);
            }
        }

        public static IEnumerator ResetPassword(
            BackendConnectionConfig config,
            string usernameOrEmail,
            string newPassword,
            Action<UnityAuthResetPasswordResponseDto> onSuccess,
            Action<string> onError)
        {
            if (config == null)
            {
                onError?.Invoke("Missing BackendConnectionConfig.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new UnityAuthResetPasswordRequestDto
            {
                usernameOrEmail = usernameOrEmail ?? string.Empty,
                newPassword = newPassword ?? string.Empty
            });

            var bodyBytes = Encoding.UTF8.GetBytes(payload);
            using (var request = new UnityWebRequest(config.BuildAuthResetPasswordUrl(), UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = config.RequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    var err = TryExtractErrorMessage(request.downloadHandler?.text, request.error);
                    onError?.Invoke(err);
                    yield break;
                }

                var responseText = request.downloadHandler.text ?? string.Empty;
                var dto = JsonUtility.FromJson<UnityAuthResetPasswordResponseDto>(responseText);
                if (dto == null)
                {
                    onError?.Invoke("Password reset failed: Invalid response from server.");
                    yield break;
                }

                onSuccess?.Invoke(dto);
            }
        }

        private static string TryExtractErrorMessage(string responseBody, string defaultError)
        {
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    var errorDto = JsonUtility.FromJson<UnityAuthErrorDto>(responseBody);
                    if (errorDto != null && !string.IsNullOrWhiteSpace(errorDto.error))
                        return errorDto.error;
                }
                catch { }
            }

            return defaultError ?? "Request failed.";
        }
    }

    [Serializable]
    public sealed class UnityAuthErrorDto
    {
        public string error;
    }

    [Serializable]
    public sealed class UnityAuthLoginRequestDto
    {
        public string username;
        public string password;
    }

    [Serializable]
    public sealed class UnityAuthLoginResponseDto
    {
        public string userId;
        public string token;
        public string canary;
        public string expiresAtUtc;
    }

    [Serializable]
    public sealed class UnityAuthRegisterRequestDto
    {
        public string username;
        public string email;
        public string password;
    }

    [Serializable]
    public sealed class UnityAuthRegisterResponseDto
    {
        public string userId;
        public string username;
        public string token;
        public string canary;
        public string expiresAtUtc;
        public string message;
    }

    [Serializable]
    public sealed class UnityAuthForgotUsernameRequestDto
    {
        public string email;
    }

    [Serializable]
    public sealed class UnityAuthForgotUsernameResponseDto
    {
        public bool success;
        public string username;
        public string email;
        public string message;
    }

    [Serializable]
    public sealed class UnityAuthResetPasswordRequestDto
    {
        public string usernameOrEmail;
        public string newPassword;
    }

    [Serializable]
    public sealed class UnityAuthResetPasswordResponseDto
    {
        public bool success;
        public string message;
    }
}

