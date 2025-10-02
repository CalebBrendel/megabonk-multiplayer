using System;
using MelonLoader;
using Steamworks;
using Megabonk.Multiplayer.Net;

namespace Megabonk.Multiplayer
{
    public static class SteamLobby
    {
        public static bool IsHost { get; private set; }
        public static CSteamID LobbyId { get; private set; }
        public static CSteamID HostId { get; private set; }

        public static event Action<bool, CSteamID, CSteamID> OnLobbyEntered; // (isHost, lobby, hostId)
        public static event Action OnLobbyLeft;

        private static Callback<LobbyCreated_t> _cbLobbyCreated;
        private static Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
        private static Callback<LobbyEnter_t> _cbLobbyEnter;
        private static Callback<SteamNetConnectionStatusChangedCallback_t> _cbConnChanged;

        // NEW: listen socket for P2P (host side)
        private static HSteamListenSocket _listen; // 0 = invalid
        private static readonly int _virtualPort = 0; // must match ConnectP2P call on client

        public static void Init()
        {
            _cbLobbyCreated  = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            _cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _cbLobbyEnter    = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _cbConnChanged   = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnChanged);

            MelonLogger.Msg("[Megabonk Multiplayer] SteamLobby.Init: callbacks ready? created={0} joinReq={1} entered={2}",
                _cbLobbyCreated != null, _cbJoinRequested != null, _cbLobbyEnter != null);
        }

        public static void Shutdown()
        {
            CloseListenSocket();
            if (LobbyId.IsValid())
            {
                SteamMatchmaking.LeaveLobby(LobbyId);
                LobbyId = CSteamID.Nil;
            }
            IsHost = false;
            HostId = CSteamID.Nil;
        }

        public static void HostLobby()
        {
            MelonLogger.Msg("[Megabonk Multiplayer] HostLobby: creating lobby…");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        }

        public static void ShowInviteOverlay()
        {
            if (!LobbyId.IsValid())
            {
                MelonLogger.Warning("[MP] Cannot invite: no lobby.");
                return;
            }
            SteamFriends.ActivateGameOverlayInviteDialog(LobbyId);
        }

        public static void LeaveLobby()
        {
            CloseListenSocket();
            if (LobbyId.IsValid())
            {
                SteamMatchmaking.LeaveLobby(LobbyId);
                LobbyId = CSteamID.Nil;
            }
            IsHost = false;
            HostId = CSteamID.Nil;
            OnLobbyLeft?.Invoke();
            MelonLogger.Msg("[Megabonk Multiplayer] Lobby left");
        }

        // ---------- Callbacks ----------
        private static void OnLobbyCreated(LobbyCreated_t ev)
        {
            if (ev.m_eResult != EResult.k_EResultOK)
            {
                MelonLogger.Error("[MP] Lobby create failed: {0}", ev.m_eResult);
                return;
            }

            LobbyId = new CSteamID(ev.m_ulSteamIDLobby);
            IsHost  = true;
            HostId  = SteamUser.GetSteamID();

            SteamMatchmaking.SetLobbyData(LobbyId, "name", SteamFriends.GetPersonaName() + "'s Lobby");
            SteamMatchmaking.SetLobbyJoinable(LobbyId, true);

            MelonLogger.Msg("[Megabonk Multiplayer] Lobby created: {0}, host={1}", LobbyId.m_SteamID, HostId.m_SteamID);
        }

        private static void OnJoinRequested(GameLobbyJoinRequested_t ev)
        {
            MelonLogger.Msg("[Megabonk Multiplayer] Join requested to lobby {0} from {1}",
                ev.m_steamIDLobby.m_SteamID, ev.m_steamIDFriend.m_SteamID);
            SteamMatchmaking.JoinLobby(ev.m_steamIDLobby);
        }

        private static void OnLobbyEnter(LobbyEnter_t ev)
        {
            LobbyId = new CSteamID(ev.m_ulSteamIDLobby);
            var owner = SteamMatchmaking.GetLobbyOwner(LobbyId);
            HostId = owner;
            IsHost = owner == SteamUser.GetSteamID();

            MelonLogger.Msg("[Megabonk Multiplayer] Lobby entered: {0}, host={1}", LobbyId.m_SteamID, HostId.m_SteamID);

            // Always enable relays for this session
            SteamNetworkingUtils.InitRelayNetworkAccess();

            // NEW: if we are the host, open a P2P listen socket on virtual port 0
            if (IsHost && _listen.m_HSteamListenSocket == 0)
            {
                var cfg = Array.Empty<SteamNetworkingConfigValue_t>();
                _listen = SteamNetworkingSockets.CreateListenSocketP2P(_virtualPort, cfg.Length, cfg);
                MelonLogger.Msg("[Megabonk Multiplayer] ListenSocketP2P created: {0} (vport={1})",
                    _listen.m_HSteamListenSocket, _virtualPort);
            }

            OnLobbyEntered?.Invoke(IsHost, LobbyId, HostId);
        }

        private static void OnConnChanged(SteamNetConnectionStatusChangedCallback_t ev)
        {
            var state = ev.m_info.m_eState;
            var hconn = ev.m_hConn;
            var hlisten = ev.m_info.m_hListenSocket;

            // Try resolve remote SteamID for logs and mapping
            CSteamID remote = new CSteamID(0);
            try
            {
                SteamNetConnectionInfo_t info;
                if (SteamNetworkingSockets.GetConnectionInfo(hconn, out info))
                    remote = info.m_identityRemote.GetSteamID();
            }
            catch { /* best-effort */ }

            MelonLogger.Msg("[Megabonk Multiplayer] ConnChanged: state={0} reason={1} from={2} debug='{3}' listen={4}",
                state, ev.m_info.m_eEndReason, remote.m_SteamID, ev.m_info.m_szEndDebug, hlisten.m_HSteamListenSocket);

            switch (state)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    // Host must accept inbound connections or they stick in Connecting forever.
                    if (IsHost)
                    {
                        if (_listen.m_HSteamListenSocket == 0)
                        {
                            // Safety: create listen socket if it somehow wasn't created yet
                            var cfg = Array.Empty<SteamNetworkingConfigValue_t>();
                            _listen = SteamNetworkingSockets.CreateListenSocketP2P(_virtualPort, cfg.Length, cfg);
                            MelonLogger.Msg("[Megabonk Multiplayer] (late) ListenSocketP2P created: {0}", _listen.m_HSteamListenSocket);
                        }

                        // Optional sanity: if event didn't report our listen socket, still accept.
                        var res = SteamNetworkingSockets.AcceptConnection(hconn);
                        if (res != EResult.k_EResultOK)
                            MelonLogger.Error("[Megabonk Multiplayer] AcceptConnection failed: {0}", res);
                        else
                            MelonLogger.Msg("[Megabonk Multiplayer] Host accepted connection from {0}", remote.m_SteamID);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    if (IsHost && remote.m_SteamID != 0)
                    {
                        NetHost.Instance?.OnNewConnection(remote, hconn);
                    }
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_None:
                    if (IsHost && remote.m_SteamID != 0)
                    {
                        NetHost.Instance?.OnDisconnect(remote);
                    }
                    // Make sure handle is closed
                    SteamNetworkingSockets.CloseConnection(hconn, 0, "cleanup", false);
                    break;
            }
        }

        private static void CloseListenSocket()
        {
            if (_listen.m_HSteamListenSocket != 0)
            {
                SteamNetworkingSockets.CloseListenSocket(_listen);
                MelonLogger.Msg("[Megabonk Multiplayer] ListenSocketP2P closed: {0}", _listen.m_HSteamListenSocket);
                _listen.m_HSteamListenSocket = 0;
            }
        }
    }
}
