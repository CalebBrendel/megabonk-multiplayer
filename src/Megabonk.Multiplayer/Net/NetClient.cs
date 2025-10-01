using System.IO;
using Steamworks;

namespace Megabonk.Multiplayer.Net
{
    public class NetClient
    {
        public static NetClient Instance { get; private set; }
        HSteamNetConnection _conn;
        Callback<SteamNetConnectionStatusChangedCallback_t> _onConnChanged;
        byte[] _rx;
        float _stateTimer;

        public static void ConnectToHost(CSteamID host)
        {
            Instance?.Shutdown();
            Instance = new NetClient();
            Instance.Init(host);
        }

        void Init(CSteamID host)
        {
            _onConnChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnChanged);
            _conn = NetCommon.ConnectTo(host);
            MelonLoader.MelonLogger.Msg($"NetClient: connecting to {host}");
        }

        public void Shutdown()
        {
            if (_conn.m_HSteamNetConnection != 0)
                SteamNetworkingSockets.CloseConnection(_conn, 0, "client shutdown", false);
            _conn = default;
            _onConnChanged = null;
            Instance = null;
        }

        static string StateName(ESteamNetworkingConnectionState s) => s.ToString();

        void OnConnChanged(SteamNetConnectionStatusChangedCallback_t ev)
        {
            if (ev.m_hConn != _conn) return;

            var info = ev.m_info;
            MelonLoader.MelonLogger.Msg(
                $"Client.ConnChanged: state={StateName(info.m_eState)} endReason={info.m_eEndReason} debug='{info.m_szEndDebug}' remote={info.m_identityRemote.GetSteamID()}");

            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    SendHello();
                    break;

                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    MelonLoader.MelonLogger.Msg("Disconnected from host");
                    Shutdown();
                    break;
            }
        }

        public void ToggleReady()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.Ready);
                w.Write(true);
                NetCommon.Send(_conn, ms.ToArray(), true);
            }
        }

        void SendHello()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.Hello);
                w.Write(SteamFriends.GetPersonaName());
                NetCommon.Send(_conn, ms.ToArray(), true);
            }
        }

        void SendPlayerState()
        {
            if (!HarmonyPatches.GameHooks.TryGetLocalPlayerPos(out var rot))
                return;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.PlayerState);
                MsgIO.WriteVec3(w, HarmonyPatches.GameHooks.LastPos);
                MsgIO.WriteQuat(w, rot);
                NetCommon.Send(_conn, ms.ToArray(), false);
            }
        }

        public void Tick()
        {
            if (_conn.m_HSteamNetConnection == 0) return;

            int got = NetCommon.Receive(_conn, ref _rx);
            if (got > 0) ProcessPacket(_rx, got);

            _stateTimer += UnityEngine.Time.deltaTime;
            if (_stateTimer >= 0.1f)
            {
                _stateTimer = 0f;
                SendPlayerState();
            }
        }

        void ProcessPacket(byte[] data, int len)
        {
            using (var ms = new System.IO.MemoryStream(data, 0, len))
            using (var r = new System.IO.BinaryReader(ms))
            {
                if (!MsgIO.ReadHeader(r, out var op)) return;
                switch (op)
                {
                    case Op.PlayerState:
                        var pos = MsgIO.ReadVec3(r);
                        var rot = MsgIO.ReadQuat(r);
                        HarmonyPatches.GameHooks.ApplyRemotePlayerState(SteamUser.GetSteamID(), pos, rot);
                        break;
                    case Op.Ready:
                        bool ready = r.ReadBoolean();
                        MelonLoader.MelonLogger.Msg($"Host ready: {ready}");
                        break;
                }
            }
        }
    }
}
