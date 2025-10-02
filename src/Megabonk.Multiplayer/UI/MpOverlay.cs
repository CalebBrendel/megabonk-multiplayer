using MelonLoader;
using UnityEngine;
using Megabonk.Multiplayer.Net;

namespace Megabonk.Multiplayer.UI
{
    // Registered via ClassInjector.RegisterTypeInIl2Cpp<MpOverlay>() in MegabonkMultiplayer.OnInitializeMelon
    public class MpOverlay : MonoBehaviour
    {
        private static MpOverlay _inst;
        private static bool _visible;

        // Window rect (screen space)
        private Rect _win = new Rect(40, 80, 440, 280);
        // Simple drag handling (avoid GUI.Window/WindowFunction)
        private bool _dragging;
        private Vector2 _dragStart;

        private string _sceneName = "GeneratedMap"; // default

        public static void Boot()
        {
            if (_inst != null) return;

            var go = new GameObject("[MP Overlay]");
            DontDestroyOnLoad(go);
            _inst = go.AddComponent<MpOverlay>();
            MelonLogger.Msg("[MP] Overlay ready (F2 to toggle).");
        }

        public static void Toggle() => _visible = !_visible;

        private void OnGUI()
        {
            if (!_visible) return;

            // Draw movable panel without GUI.Window (no WindowFunction delegate)
            DrawPanel();
        }

        private void DrawPanel()
        {
            // Panel background
            GUI.depth = 0;
            GUI.color = Color.white;

            // Group everything so local coordinates start at (0,0)
            GUI.BeginGroup(_win);

            // Backing box
            GUI.Box(new Rect(0, 0, _win.width, _win.height), GUIContent.none);

            // Title bar
            var titleBar = new Rect(0, 0, _win.width, 24f);
            GUI.Box(titleBar, "Megabonk Multiplayer (Mod)");

            // Close button
            var closeRect = new Rect(_win.width - 26f, 2f, 22f, 20f);
            if (GUI.Button(closeRect, "X"))
            {
                _visible = false;
                GUI.EndGroup();
                return;
            }

            // Dragging (simple)
            var e = Event.current;
            if (e != null)
            {
                if (e.type == EventType.MouseDown && titleBar.Contains(e.mousePosition))
                {
                    _dragging = true;
                    _dragStart = e.mousePosition;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && _dragging)
                {
                    // convert local delta to screen movement
                    _win.x += e.delta.x;
                    _win.y += e.delta.y;
                    e.Use();
                }
                else if (e.type == EventType.MouseUp && _dragging)
                {
                    _dragging = false;
                    e.Use();
                }
            }

            // Content area
            GUILayout.BeginArea(new Rect(8, 28, _win.width - 16, _win.height - 36));
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
                    NetHost.Instance.StartCoop(_sceneName);
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

            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUI.EndGroup();
        }
    }
}
