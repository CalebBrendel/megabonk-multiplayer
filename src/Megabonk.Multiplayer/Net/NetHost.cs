using System.Collections.Generic;
using System.IO;
using Steamworks;

namespace Megabonk.Multiplayer.Net
{
    public class NetHost
    {
        public static NetHost Instance { get; private set; }

        HSteamListenSocket _listen;
        readonly List<HSteamNetConnection> _clients = new List<HSteamNetConnection>();
        readonly Dictionary<HSteamNetConnection, CSteamID> _clientIds = new Dictionary<HSteamNetConnection, CSteamID>();
        Callback<SteamNetConnectionStatusChangedCallback_t> _onConnChanged;
        float _stateTimer;
        byte[] _rx;
        bool _everyoneReady;

        public static void StartListening()
        {
            Instance?.Shutdown();
            Instance = new NetHost();
            Instance.Init();
        }

        void Init()
        {
            _listen = NetCommon.ListenP2P();
            _onConnChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnConnChanged);
            MelonLoader.MelonLogger.Msg("NetHost: listening P2P");
        }

        public void Shutdown()
        {
            foreach (var c in _clients)
                SteamNetworkingSockets.CloseConnection(c, 0, "host shutdown", false);
            _clients.Clear();
            _clientIds.Clear();

            if (_listen.m_HSteamListenSocket != 0)
                SteamNetworkingSockets.CloseListenSocket(_listen);
            _listen = default;

            _onConnChanged = null;
            Instance = null;
        }

        static string StateName(ESteamNetworkingConnectionState s) => s.ToString();

        void LogConnInfo(string who, HSteamNetConnection h, SteamNetConnectionInfo_t info)
        {
            MelonLoader.MelonLogger.Msg($"{who}: state={StateName(info.m_eState)} endReason={info.m_eEndReason} debug='{info.m_szEndDebug}' from={info.m_identityRemote.GetSteamID()}");
        }

        void OnConnChanged(SteamNetConnectionStatusChangedCallback_t ev)
        {
            var info = ev.m_info;
            LogConnInfo("Host.ConnChanged", ev.m_hConn, info);

            switch (info.m_eState)
            {
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                    SteamNetworkingSockets.AcceptConnection(ev.m_hConn);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                    _clients.Add(ev.m_hConn);
                    _clientIds[ev.m_hConn] = info.m_identityRemote.GetSteamID();
                    SendHello(ev.m_hConn);
                    break;
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                    _clients.Remove(ev.m_hConn);
                    _clientIds.Remove(ev.m_hConn);
                    SteamNetworkingSockets.CloseConnection(ev.m_hConn, 0, "disconnect", false);
                    break;
            }
        }

        public void BroadcastLoadLevel(string sceneName)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.LoadLevel);
                w.Write(sceneName);
                var bytes = ms.ToArray();
                foreach (var c in _clients) NetCommon.Send(c, bytes, true);
            }
            MelonLoader.MelonLogger.Msg($"Host: told clients to load scene '{sceneName}'");
        }

        public void ToggleReady()
        {
            _everyoneReady = !_everyoneReady;
            BroadcastReady(_everyoneReady);
        }

        void SendHello(HSteamNetConnection c)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.Hello);
                w.Write(SteamFriends.GetPersonaName());
                NetCommon.Send(c, ms.ToArray(), true);
            }
        }

        void BroadcastReady(bool ready)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.Ready);
                w.Write(ready);
                var bytes = ms.ToArray();
                foreach (var c in _clients) NetCommon.Send(c, bytes, true);
            }
        }

        void BroadcastPlayerState()
        {
            // still gated off until we wire a cached Transform
            if (!HarmonyPatches.GameHooks.TryGetLocalPlayerPos(out var rot))
                return;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                MsgIO.WriteHeader(w, Op.PlayerState);
                MsgIO.WriteVec3(w, HarmonyPatches.GameHooks.LastPos);
                MsgIO.WriteQuat(w, rot);
                var bytes = ms.ToArray();
                foreach (var c in _clients) NetCommon.Send(c, bytes, false);
            }
        }

        public void Tick()
        {
            foreach (var c in _clients.ToArray())
            {
                int got = NetCommon.Receive(c, ref _rx);
                if (got > 0) ProcessPacket(c, _rx, got);
            }

            _stateTimer += UnityEngine.Time.deltaTime;
            if (_stateTimer >= 0.1f)
            {
                _stateTimer = 0f;
                BroadcastPlayerState();
            }
        }

        void ProcessPacket(HSteamNetConnection from, byte[] data, int len)
        {
            using (var ms = new MemoryStream(data, 0, len))
            using (var r = new BinaryReader(ms))
            {
                if (!MsgIO.ReadHeader(r, out var op)) return;
                switch (op)
                {
                    case Op.Hello:
                    {
                        var name = r.ReadString();
                        MelonLoader.MelonLogger.Msg($"Host: got HELLO from {_clientIds[from]} ({name})");
                        break;
                    }
                    case Op.PlayerState:
                    {
                        var pos = MsgIO.ReadVec3(r);
                        var rot = MsgIO.ReadQuat(r);
                        HarmonyPatches.GameHooks.ApplyRemotePlayerState(_clientIds[from], pos, rot);
                        break;
                    }
                    case Op.Ready:
                    {
                        bool ready = r.ReadBoolean();
                        MelonLoader.MelonLogger.Msg($"Client ready: {ready}");
                        break;
                    }
                }
            }
        }
    }
}
