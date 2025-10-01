using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.Net;

[assembly: MelonInfo(typeof(Megabonk.Multiplayer.MegabonkMultiplayer), "Megabonk Multiplayer", "0.2.3", "Caleb Brendel")]
[assembly: MelonGame(null, "Megabonk")]

namespace Megabonk.Multiplayer
{
    public class MegabonkMultiplayer : MelonMod
    {
        private static bool _steamOk;
        private HarmonyLib.Harmony _harmony;

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

            if (Input.GetKeyDown(KeyCode.F9))  SteamLobby.HostLobby();
            if (Input.GetKeyDown(KeyCode.F10)) SteamLobby.ShowInviteOverlay();
            if (Input.GetKeyDown(KeyCode.F11)) SteamLobby.LeaveLobby();

            // Host: broadcast current scene to clients
            if (Input.GetKeyDown(KeyCode.F6) && IsHost && NetHost.Instance != null)
            {
                var scene = SceneManager.GetActiveScene().name;
                MelonLogger.Msg($"Host: starting co-op on scene '{scene}'");
                NetHost.Instance.BroadcastLoadLevel(scene);
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
