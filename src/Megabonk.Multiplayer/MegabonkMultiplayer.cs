using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.HarmonyPatches;   // GameHooks
using Megabonk.Multiplayer.Net;
using Megabonk.Multiplayer.Debugging;        // SceneDumper (F5)

[assembly: MelonInfo(typeof(Megabonk.Multiplayer.MegabonkMultiplayer), "Megabonk Multiplayer", "0.3.4", "CalebB")]
[assembly: MelonGame(null, "Megabonk")]

namespace Megabonk.Multiplayer
{
    public class MegabonkMultiplayer : MelonMod
    {
        private static bool _steamOk;
        private HarmonyLib.Harmony _harmony;

        // scene-change tracker (avoids UnityAction<T1,T2> AOT issues)
        private static int _lastSceneIndex = -1;
        private static string _lastSceneName = null;

        public static bool IsHost => SteamLobby.IsHost;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Megabonk Multiplayer init...");
            _harmony = new HarmonyLib.Harmony("cb.megabonk.multiplayer");
            _harmony.PatchAll();

            InitSteam();

            if (_steamOk)
            {
                SteamLobby.OnLobbyEntered += LobbyEntered;
                SteamLobby.OnLobbyLeft += LobbyLeft;
                SteamLobby.Init();

                _lastSceneIndex = -1;
                _lastSceneName = null;
            }
            else
            {
                MelonLogger.Error("SteamAPI.Init failed; multiplayer disabled this session.");
            }
        }

        private void InitSteam()
        {
            if (!Steamworks.Packsize.Test() || !Steamworks.DllCheck.Test())
            {
                MelonLogger.Error("Steamworks.NET Packsize/DllCheck failed.");
                return;
            }
            _steamOk = Steamworks.SteamAPI.Init();
            if (!_steamOk)
            {
                MelonLogger.Error("SteamAPI.Init failed. Is Steam running / app owned?");
                return;
            }

            Steamworks.SteamNetworkingUtils.InitRelayNetworkAccess();
            MelonLogger.Msg("SteamNetworking: Relay network access requested.");
        }

        public override void OnUpdate()
        {
            if (!_steamOk) return;

            Steamworks.SteamAPI.RunCallbacks();

            // Scene change poll (replaces SceneManager.sceneLoaded subscription)
            var scn = SceneManager.GetActiveScene();
            if (scn.buildIndex != _lastSceneIndex || scn.name != _lastSceneName)
            {
                _lastSceneIndex = scn.buildIndex;
                _lastSceneName = scn.name;
                GameHooks.OnSceneLoaded(scn, LoadSceneMode.Single);
            }

            // Hotkeys
            if (Input.GetKeyDown(KeyCode.F9))  SteamLobby.HostLobby();
            if (Input.GetKeyDown(KeyCode.F10)) SteamLobby.ShowInviteOverlay();
            if (Input.GetKeyDown(KeyCode.F11)) SteamLobby.LeaveLobby();

            // Host: broadcast current scene to clients
            if (Input.GetKeyDown(KeyCode.F6) && IsHost && NetHost.Instance != null)
            {
                var scene = scn.name;
                MelonLogger.Msg($"Host: starting co-op on scene '{scene}'");
                NetHost.Instance.BroadcastLoadLevel(scene);
            }

                // Manual player bind if auto-bind missed it
            if (Input.GetKeyDown(KeyCode.F8))
            {
                var ok = GameHooks.ForceRebind(verbose: true);
                if (!ok) MelonLogger.Msg("[MP] Rebind attempt didn't find a player yet. Try again after the scene fully loads.");
            }

            // Scene dumper (find the real player root) — now on F5
            if (Input.GetKeyDown(KeyCode.F5))
            {
                SceneDumper.Dump(3); // raise to 4–5 for deeper trees
            }

            if (Input.GetKeyDown(KeyCode.F7))
            {
                if (IsHost) NetHost.Instance?.ToggleReady();
                else NetClient.Instance?.ToggleReady();
            }

            if (IsHost) NetHost.Instance?.Tick(); else NetClient.Instance?.Tick();
        }

        public override void OnDeinitializeMelon()
        {
            NetHost.Instance?.Shutdown();
            NetClient.Instance?.Shutdown();
            SteamLobby.Shutdown();
            if (_steamOk) Steamworks.SteamAPI.Shutdown();
            _harmony?.UnpatchSelf();
        }

        private void LobbyEntered(bool isHost, Steamworks.CSteamID lobby, Steamworks.CSteamID hostId)
        {
            MelonLogger.Msg($"Lobby entered: {lobby}, host={hostId}");
            if (isHost) NetHost.StartListening(); else NetClient.ConnectToHost(hostId);
        }

        private void LobbyLeft()
        {
            MelonLogger.Msg("Lobby left");
            NetHost.Instance?.Shutdown();
            NetClient.Instance?.Shutdown();
        }
    }
}
