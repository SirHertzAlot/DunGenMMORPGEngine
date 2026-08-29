using UnityEngine;
using UnityEngine.Networking;

namespace DunGen.Networking
{
    [CreateAssetMenu(fileName = "BackendConnectionConfig", menuName = "DunGen/Networking/Backend Connection Config")]
    public sealed class BackendConnectionConfig : ScriptableObject
    {
        [SerializeField] private string authoritativeBaseUrl = "http://127.0.0.1:8081";
        [SerializeField] private string adminUiBaseUrl = "http://127.0.0.1:8083";
        [SerializeField] private string adminApiKey = "dev-admin-key";
        [SerializeField] private float pollIntervalSeconds = 1f;
        [SerializeField] private int requestTimeoutSeconds = 10;
        [SerializeField] private string defaultSessionId = "session-001";
        [SerializeField] private string clientPayloadPepper = "dev-client-pepper";

        public string AuthoritativeBaseUrl => authoritativeBaseUrl.TrimEnd('/');
        public string AdminUiBaseUrl => string.IsNullOrWhiteSpace(adminUiBaseUrl) ? "http://127.0.0.1:8083" : adminUiBaseUrl.TrimEnd('/');
        public string AdminApiKey => adminApiKey;
        public float PollIntervalSeconds => Mathf.Max(0.25f, pollIntervalSeconds);
        public int RequestTimeoutSeconds => Mathf.Clamp(requestTimeoutSeconds, 2, 120);
        public string DefaultSessionId => string.IsNullOrWhiteSpace(defaultSessionId) ? "session-001" : defaultSessionId.Trim();
        public string ClientPayloadPepper => string.IsNullOrWhiteSpace(clientPayloadPepper) ? "dev-client-pepper" : clientPayloadPepper;

        public string BuildAuthLoginUrl()
        {
            return $"{AuthoritativeBaseUrl}/v1/auth/login";
        }

        public string BuildAuthRegisterUrl()
        {
            return $"{AuthoritativeBaseUrl}/v1/auth/register";
        }

        public string BuildAuthForgotUsernameUrl()
        {
            return $"{AuthoritativeBaseUrl}/v1/auth/forgot-username";
        }

        public string BuildAuthResetPasswordUrl()
        {
            return $"{AuthoritativeBaseUrl}/v1/auth/reset-password";
        }

        public string BuildClientBootstrapUrl(string sessionId)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            return $"{AuthoritativeBaseUrl}/client/sessions/{escapedSessionId}/world/bootstrap";
        }

        public string BuildClientTimelineUrl(string sessionId, int take = 200)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            return $"{AuthoritativeBaseUrl}/client/sessions/{escapedSessionId}/timeline?take={Mathf.Max(1, take)}";
        }

        public string BuildClientWorldUrl(string sessionId)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            return $"{AuthoritativeBaseUrl}/client/sessions/{escapedSessionId}/world/current";
        }

        public string BuildMasteryOfferUrl(string userId, string itemType, string masteryTier)
        {
            var escapedUserId = UnityWebRequest.EscapeURL(userId ?? string.Empty);
            var escapedItemType = UnityWebRequest.EscapeURL(itemType ?? string.Empty);
            var escapedTier = UnityWebRequest.EscapeURL(masteryTier ?? string.Empty);
            return $"{AuthoritativeBaseUrl}/v1/mastery/offers?userId={escapedUserId}&itemType={escapedItemType}&masteryTier={escapedTier}";
        }

        public string BuildMasterySelectUrl(string userId, string offerId, string skillId)
        {
            var escapedUserId = UnityWebRequest.EscapeURL(userId ?? string.Empty);
            var escapedOfferId = UnityWebRequest.EscapeURL(offerId ?? string.Empty);
            var escapedSkillId = UnityWebRequest.EscapeURL(skillId ?? string.Empty);
            return $"{AuthoritativeBaseUrl}/v1/mastery/select?userId={escapedUserId}&offerId={escapedOfferId}&skillId={escapedSkillId}";
        }

        public string BuildMasteryProgressUrl(string userId, string itemType)
        {
            var escapedUserId = UnityWebRequest.EscapeURL(userId ?? string.Empty);
            var escapedItemType = UnityWebRequest.EscapeURL(itemType ?? string.Empty);
            return $"{AuthoritativeBaseUrl}/v1/mastery/progress?userId={escapedUserId}&itemType={escapedItemType}";
        }

        public string BuildAdminUiUrl(string sessionId)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            var escapedAdminKey = UnityWebRequest.EscapeURL(AdminApiKey ?? string.Empty);
            return $"{AdminUiBaseUrl}?sessionId={escapedSessionId}&adminKey={escapedAdminKey}";
        }

        public string BuildPoolClaimUrl(int difficultyLevel)
        {
            return $"{AuthoritativeBaseUrl}/v1/pool/claim?difficultyLevel={Mathf.Max(1, difficultyLevel)}";
        }

        public string BuildPoolStatusUrl()
        {
            return $"{AuthoritativeBaseUrl}/v1/pool/status";
        }

        public string BuildWorldSnapshotUrl(string sessionId)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            return $"{AuthoritativeBaseUrl}/v1/world/sessions/{escapedSessionId}/snapshot";
        }

        public string BuildBinarySnapshotUrl(string sessionId)
        {
            var escapedSessionId = UnityWebRequest.EscapeURL(string.IsNullOrWhiteSpace(sessionId) ? DefaultSessionId : sessionId.Trim());
            return $"{AuthoritativeBaseUrl}/v1/world/sessions/{escapedSessionId}/binary-snapshot";
        }
    }
}
