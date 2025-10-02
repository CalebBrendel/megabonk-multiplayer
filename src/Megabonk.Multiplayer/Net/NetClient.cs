using System;
using System.IO;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer.Net
{
    public class NetClient
    {
        public static NetClient Instance { get; private set; }

        private HSteamNetConnection _conn;
        private readonly MemoryStream _ms = new MemoryStream(256);
        private readonly BinaryWriter _w;
        private float _lastSendTime;

        private bool _readySelf;
        private int _seed;

        public static void ConnectToHost(CSteamID host)
        {
            Instance?.Shutdown();
            Instance = new NetClient();
            Instance.Init(host);
        }

        private NetClient()
        {
            _w = new BinaryWriter(_ms);
        }

        private void Init(CSteamID host)
        {
            var cfg = new SteamNetworkingConfigValue_t[0];
            var ident = new SteamNetworkingIdentity();
            ident.SetSteamID(host);
            _conn = SteamNetworkingSockets.ConnectP2P(ref ident, 0, cfg.Length, cfg);
            MelonLogger.Msg("[Megabonk Multiplayer] Client connecting to host...");
        }

        public void Shutdown()
        {
            if (_conn != 0)
                SteamNetworkingSockets.CloseConnection(_conn, 1000, "client shutdown", false);
            _conn = 0;
            Instance = null;
        }

        public void ToggleReady()
        {
            _readySelf = !_readySelf;
            MelonLogger.Msg($"[MP][Client] Ready = {_readySelf}");
            SendReady(_readySelf);
        }

        public void OnMsg(Op op, BinaryReader r)
        {
            switch (op)
            {
                case Op.Hello:
                    SendHelloAck();
                    break;
                case Op.SeedSync:
                    _seed = r.ReadInt32();
                    UnityEngine.Random.InitState(_seed);
                    MelonLogger.Msg($"[MP][Client] Seed synced: {_seed}");
                    break;
                case Op.LoadLevel:
                    var scene = MsgIO.ReadString(r);
                    MelonLogger.Msg($"[MP][Client] Loading scene '{scene}' (seed={_seed})");
                    SceneManager.LoadScene(scene);
                    break;
                case Op.PlayerState:
                {
                    var pos = MsgIO.ReadVec3(r);
                    var rot = MsgIO.ReadQuat(r);
                    GameHooks.ApplyRemotePlayerState(SteamUser.GetSteamID(), pos, rot);
                    break;
                }
                default:
                    break;
            }
        }

        private void SendHelloAck()
        {
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.Hello);
            _w.Flush();
            SendMsg(_ms);
        }

        private void SendReady(bool val)
        {
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.Ready);
            _w.Write(val);
            _w.Flush();
            SendMsg(_ms);
        }

        private void SendMsg(MemoryStream ms)
        {
            if (_conn == 0) return;
            var len = (int)ms.Length;
            var buf = ms.GetBuffer();
            var send = new SteamNetworkingMessage_t();
            send.m_pData = System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(buf, 0);
            send.m_cbSize = len;
            send.m_conn = _conn;
            send.m_nFlags = 0;
            SteamNetworkingSockets.SendMessages(1, new[] { send }, out var _);
        }

        public void Tick()
        {
            var now = Time.unscaledTime;
            if (now - _lastSendTime < 0.1f) return; // ~10 Hz
            _lastSendTime = now;

            if (!HarmonyPatches.GameHooks.TryGetLocalPlayerPos(out var rot)) return;
            var pos = HarmonyPatches.GameHooks.LastPos;

            // send PlayerState to host
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.PlayerState);
            MsgIO.WriteVec3(_w, pos);
            MsgIO.WriteQuat(_w, rot);
            _w.Flush();
            SendMsg(_ms);
        }
    }
}
