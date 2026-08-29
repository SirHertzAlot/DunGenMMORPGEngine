using UnityEngine;

namespace DunGen.Networking
{
    /// <summary>
    /// Lightweight IMGUI overlay for inspecting the authoritative session state in play mode.
    /// Kept in the networking layer so it does not introduce cross-domain compile dependencies.
    /// </summary>
    public sealed class AuthoritativeSessionDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;
        [SerializeField] private Rect panelRect = new(520f, 10f, 460f, 320f);

        private AuthoritativeSessionStateStore _stateStore;

        private void OnGUI()
        {
            if (!visible)
                return;

            _stateStore ??= GetComponent<AuthoritativeSessionStateStore>();
            if (_stateStore == null)
                return;

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("=== AUTHORITATIVE SESSION ===", GUI.skin.box);
            GUILayout.Label($"Connected: {_stateStore.IsConnected}", GUI.skin.box);
            GUILayout.Label($"Session: {_stateStore.SessionId}", GUI.skin.box);
            GUILayout.Label($"World Ready: {_stateStore.HasWorld}", GUI.skin.box);
            GUILayout.Label($"Execution: {_stateStore.ExecutionId}", GUI.skin.box);
            GUILayout.Label($"Counts: rooms={_stateStore.RoomCount}, enemies={_stateStore.EnemyCount}, loot={_stateStore.LootCount}", GUI.skin.box);

            if (_stateStore.HasWorldSnapshot)
            {
                GUILayout.Label($"World: {_stateStore.WorldWidth}x{_stateStore.WorldHeight}, level={_stateStore.WorldDungeonLevel}, seed={_stateStore.WorldSeed}", GUI.skin.box);
            }

            if (!string.IsNullOrWhiteSpace(_stateStore.LastError))
                GUILayout.Label($"Last Error: {_stateStore.LastError}", GUI.skin.box);

            var timeline = _stateStore.RecentTimeline;
            if (timeline.Count > 0)
            {
                GUILayout.Label("Recent Timeline:", GUI.skin.box);
                for (int i = timeline.Count - 1; i >= 0 && i >= timeline.Count - 5; i--)
                {
                    var entry = timeline[i];
                    GUILayout.Label($"[{entry.Category}] {entry.EventType}: {entry.Message}", GUI.skin.box);
                }
            }

            GUILayout.EndArea();
        }
    }
}