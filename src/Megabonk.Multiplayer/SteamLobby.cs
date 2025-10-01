using Steamworks;
using UnityEngine;

namespace Megabonk.Multiplayer
{
    public static class SteamLobby
    {
        public static bool IsHost { get; private set; }
        public static CSteamID CurrentLobby { get; private set; }
        public static CSteamID HostId { get; private set; }

        public static event System.Action<bool, CSteamID, CSteamID> OnLobbyEntered;
        public static event System.Action OnLobbyLeft;

        static Callback<GameLobbyJoinRequested_t> _onLobbyJoinRequested;
        static Callback<LobbyEnter_t> _onLobbyEntered;
        static CallResult<LobbyCreated_t> _onLobbyCreated;

        public static void Init()
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogError("SteamLobby.Init: Steam is not running or SteamAPI not initialized.");
                return;
            }

            _onLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
            _onLobbyEntered       = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCb);
            _onLobbyCreated       = CallResult<LobbyCreated_t>.Create(OnLobbyCreatedCb);

            Debug.Log($"SteamLobby.Init: callbacks ready? created={_onLobbyCreated != null}, joinReq={_onLobbyJoinRequested != null}, entered={_onLobbyEntered != null}");
        }

        public static void Shutdown()
        {
            LeaveLobby();
            _onLobbyJoinRequested = null;
            _onLobbyEntered = null;
            _onLobbyCreated = null;
        }

        public static void HostLobby(int maxPlayers = 4, ELobbyType type = ELobbyType.k_ELobbyTypeFriendsOnly)
        {
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogError("HostLobby: SteamAPI not running; abort.");
                return;
            }
            if (_onLobbyCreated == null)
            {
                Debug.LogError("HostLobby: _onLobbyCreated is null; did Init() run?");
                return;
            }

            Debug.Log("HostLobby: creating lobby…");
            var call = SteamMatchmaking.CreateLobby(type, maxPlayers);
            _onLobbyCreated.Set(call);
        }

        public static void LeaveLobby()
        {
            if (CurrentLobby.IsValid())
            {
                SteamMatchmaking.LeaveLobby(CurrentLobby);
                CurrentLobby = default;
                OnLobbyLeft?.Invoke();
            }
        }

        public static void ShowInviteOverlay()
        {
            if (CurrentLobby.IsValid())
                SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        }

        static void OnLobbyCreatedCb(LobbyCreated_t data, bool ioFail)
        {
            if (ioFail || data.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"Lobby creation failed: {data.m_eResult}");
                return;
            }
            CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
            IsHost = true;
            HostId = SteamUser.GetSteamID();
            SteamMatchmaking.SetLobbyJoinable(CurrentLobby, true);
            SteamMatchmaking.SetLobbyOwner(CurrentLobby, HostId);
            SteamFriends.SetRichPresence("connect", CurrentLobby.m_SteamID.ToString());
            ShowInviteOverlay();
        }

        static void OnLobbyEnteredCb(LobbyEnter_t data)
        {
            CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
            HostId = SteamMatchmaking.GetLobbyOwner(CurrentLobby);
            IsHost = (HostId == SteamUser.GetSteamID());
            OnLobbyEntered?.Invoke(IsHost, CurrentLobby, HostId);
        }

        static void OnLobbyJoinRequested(GameLobbyJoinRequested_t data)
        {
            SteamMatchmaking.JoinLobby(data.m_steamIDLobby);
        }
    }
}
