using UnityEngine;

namespace DunGen.Networking
{
    public sealed class ReactAdminPanelLauncher : MonoBehaviour
    {
        [SerializeField] private BackendConnectionConfig connectionConfig;
        [SerializeField] private string sessionIdOverride = "";
        [SerializeField] private bool useStandaloneAdminUi = true;
        [SerializeField] private string standaloneAdminUiUrl = "http://localhost:8083";
        [SerializeField] private MonoBehaviour panelHostBehaviour;

        public void OpenDashboard()
        {
            if (connectionConfig == null)
            {
                Debug.LogError("ReactAdminPanelLauncher requires a BackendConnectionConfig asset.");
                return;
            }

            var sessionId = string.IsNullOrWhiteSpace(sessionIdOverride)
                ? connectionConfig.DefaultSessionId
                : sessionIdOverride.Trim();

            var url = BuildDashboardUrl(sessionId);
            if (panelHostBehaviour is IReactAdminPanelHost panelHost)
            {
                panelHost.Open(url);
                return;
            }

            Application.OpenURL(url);
        }

        private string BuildDashboardUrl(string sessionId)
        {
            if (useStandaloneAdminUi)
            {
                var standaloneUrl = string.IsNullOrWhiteSpace(standaloneAdminUiUrl)
                    ? "http://localhost:8083"
                    : standaloneAdminUiUrl.Trim();
                return standaloneUrl;
            }

            return $"{connectionConfig.AuthoritativeBaseUrl}/admin/observability/dashboard?sessionId={sessionId}&adminKey={connectionConfig.AdminApiKey}";
        }
    }
}
