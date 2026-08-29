#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Authoritative.Services
{
    public sealed class UserAccount
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public interface IUserAccountService
    {
        bool Register(string username, string email, string password, out UserAccount? account, out string error);
        bool ValidateCredentials(string usernameOrEmail, string password, out UserAccount? account, out string error);
        bool ForgotUsername(string email, out string? username, out string error);
        bool ResetPassword(string usernameOrEmail, string newPassword, out string error);
    }

    public sealed class UserAccountService : IUserAccountService
    {
        private readonly ConcurrentDictionary<string, UserAccount> _usersByUsername = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, string> _usernameByEmail = new(StringComparer.OrdinalIgnoreCase);

        public UserAccountService()
        {
            // Seed with default development/test account
            Register("test", "test@dungen.local", "test", out _, out _);
        }

        public bool Register(string username, string email, string password, out UserAccount? account, out string error)
        {
            account = null;
            error = string.Empty;

            var trimmedUsername = username?.Trim() ?? string.Empty;
            var trimmedEmail = email?.Trim() ?? string.Empty;
            var trimmedPassword = password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(trimmedUsername) || trimmedUsername.Length < 3)
            {
                error = "Username must be at least 3 characters long.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(trimmedEmail) || !trimmedEmail.Contains('@') || !trimmedEmail.Contains('.'))
            {
                error = "A valid email address is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(trimmedPassword) || trimmedPassword.Length < 4)
            {
                error = "Password must be at least 4 characters long.";
                return false;
            }

            if (_usersByUsername.ContainsKey(trimmedUsername))
            {
                error = $"Username '{trimmedUsername}' is already taken.";
                return false;
            }

            if (_usernameByEmail.ContainsKey(trimmedEmail))
            {
                error = $"An account with email '{trimmedEmail}' already exists.";
                return false;
            }

            var saltBytes = RandomNumberGenerator.GetBytes(16);
            var salt = Convert.ToHexString(saltBytes).ToLowerInvariant();
            var hash = HashPassword(trimmedPassword, salt);

            var newAccount = new UserAccount
            {
                UserId = Guid.NewGuid().ToString("N"),
                Username = trimmedUsername,
                Email = trimmedEmail,
                Salt = salt,
                PasswordHash = hash,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            if (!_usersByUsername.TryAdd(trimmedUsername, newAccount))
            {
                error = "Failed to create user account.";
                return false;
            }

            _usernameByEmail[trimmedEmail] = trimmedUsername;
            account = newAccount;
            return true;
        }

        public bool ValidateCredentials(string usernameOrEmail, string password, out UserAccount? account, out string error)
        {
            account = null;
            error = string.Empty;

            var input = usernameOrEmail?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrEmpty(password))
            {
                error = "Username and password are required.";
                return false;
            }

            UserAccount? target = null;
            if (_usersByUsername.TryGetValue(input, out var byUser))
            {
                target = byUser;
            }
            else if (_usernameByEmail.TryGetValue(input, out var un) && _usersByUsername.TryGetValue(un, out var byEmail))
            {
                target = byEmail;
            }

            if (target == null)
            {
                error = "Invalid username or password.";
                return false;
            }

            var computed = HashPassword(password, target.Salt);
            if (!ConstantTimeEquals(computed, target.PasswordHash))
            {
                error = "Invalid username or password.";
                return false;
            }

            account = target;
            return true;
        }

        public bool ForgotUsername(string email, out string? username, out string error)
        {
            username = null;
            error = string.Empty;

            var trimmedEmail = email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedEmail))
            {
                error = "Email is required.";
                return false;
            }

            if (!_usernameByEmail.TryGetValue(trimmedEmail, out var foundUsername))
            {
                error = $"No account found associated with email '{trimmedEmail}'.";
                return false;
            }

            username = foundUsername;
            return true;
        }

        public bool ResetPassword(string usernameOrEmail, string newPassword, out string error)
        {
            error = string.Empty;

            var input = usernameOrEmail?.Trim() ?? string.Empty;
            var trimmedPassword = newPassword ?? string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Username or email is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(trimmedPassword) || trimmedPassword.Length < 4)
            {
                error = "New password must be at least 4 characters long.";
                return false;
            }

            UserAccount? target = null;
            if (_usersByUsername.TryGetValue(input, out var byUser))
            {
                target = byUser;
            }
            else if (_usernameByEmail.TryGetValue(input, out var un) && _usersByUsername.TryGetValue(un, out var byEmail))
            {
                target = byEmail;
            }

            if (target == null)
            {
                error = "Account not found.";
                return false;
            }

            var newSaltBytes = RandomNumberGenerator.GetBytes(16);
            var newSalt = Convert.ToHexString(newSaltBytes).ToLowerInvariant();
            var newHash = HashPassword(trimmedPassword, newSalt);

            target.Salt = newSalt;
            target.PasswordHash = newHash;
            target.UpdatedAtUtc = DateTime.UtcNow;

            return true;
        }

        private static string HashPassword(string password, string salt)
        {
            var combined = Encoding.UTF8.GetBytes($"{salt}:{password}");
            var hash = SHA256.HashData(combined);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }

    public sealed class ClientRegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ClientRegisterResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Canary { get; set; } = string.Empty;
        public string ExpiresAtUtc { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ClientForgotUsernameRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public sealed class ClientForgotUsernameResponse
    {
        public bool Success { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public sealed class ClientResetPasswordRequest
    {
        public string UsernameOrEmail { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public sealed class ClientResetPasswordResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
#endif
