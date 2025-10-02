using MelonLoader;
using UnityEngine;
using Megabonk.Multiplayer.Net;

namespace Megabonk.Multiplayer.UI
{
    public class MpOverlay : MonoBehaviour
    {
        private static MpOverlay _inst;
        private static bool _visible;
        private Rect _win = new Rect(40, 80, 420, 260);
        private string _sceneName = "GeneratedMap"; // default; matches the log you shared

        public static void Boot()
        {
            if (_inst != null) return;
            var go = new GameObject("[MP Overlay]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<MpOverlay>();
            MelonLogger.Msg("[MP] Overlay ready (F2 to toggle).");
        }

        public static void Toggle() => _visible = !_visible;

        void OnGUI()
        {
            if (!_visible) return;

            _win = GUI.Window(43210, _win, DrawWin, "Megabonk Multiplayer (Mod)");
        }

        void DrawWin(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.Label(SteamLobby.IsHost ? "Role: Host" : "Role: Client");

            if (SteamLobby.IsHost)
            {
                if (GUILayout.Button("Invite via Steam"))
                    SteamLobby.ShowInviteOverlay();

                var (ready, total) = NetHost.Instance != null ? NetHost.Instance.GetReadyCounts() : (0, 0);
                GUILayout.Label($"Ready: {ready}/{total}");

                GUILayout.Space(6);
                GUILayout.Label("Target Scene:");
                _sceneName = GUILayout.TextField(_sceneName);

                GUI.enabled = NetHost.Instance != null && NetHost.Instance.AllReady();
                if (GUILayout.Button("Start Co-op"))
                {
                    NetHost.Instance.StartCoop(_sceneName);
                }
                GUI.enabled = true;

                if (GUILayout.Button("Toggle Ready (Host)"))
                    NetHost.Instance?.ToggleReady();
            }
            else
            {
                if (GUILayout.Button("Toggle Ready"))
                    NetClient.Instance?.ToggleReady();

                GUILayout.Space(6);
                GUILayout.Label("Waiting for host to start…");
            }

            GUILayout.Space(10);
            GUILayout.Label("Hotkeys: F9 Host, F10 Invite, F11 Leave, F6 Sync Scene, F8 Rebind, F5 Dump");
            if (GUILayout.Button("Close")) _visible = false;

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}
