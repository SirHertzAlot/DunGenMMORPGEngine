using UnityEngine;

namespace DunGen.Networking
{
    public interface IReactAdminPanelHost
    {
        void Open(string url);
    }

    public sealed class BrowserReactAdminPanelHost : MonoBehaviour, IReactAdminPanelHost
    {
        public void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            Application.OpenURL(url);
        }
    }
}
