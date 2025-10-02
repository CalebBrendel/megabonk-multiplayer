using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.HarmonyPatches; // GameHooks

namespace Megabonk.Multiplayer.Net
{
    public class NetClient
    {
        public static NetClient Instance { get; private set; }

        // Store the actual handle type used by your Steamworks.NET build
        private HSteamNetConnection _conn;

        private readonly MemoryStream _ms = new MemoryStream(256);
        private readonly BinaryWriter _w;

        private float _lastSendTime;
        private bool _readySelf;
        private int _seed;

        private CSteamID _hostId;

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
            _hostId = host;

            var cfg = Array.Empty<SteamNetworkingConfigValue_t>();
            var ident = new SteamNetworkingIdentity();
            ident.SetSteamID(host);

            // Your Steamworks.NET signature returns HSteamNetConnection
            _conn = SteamNetworkingSockets.ConnectP2P(ref ident, 0, cfg.Length, cfg);
            MelonLogger.Msg("[Megabonk Multiplayer] Client connecting to host...");
        }

        private static bool IsValid(HSteamNetConnection h) => h.m_HSteamNetConnection != 0;

        public void Shutdown()
        {
            if (IsValid(_conn))
                SteamNetworkingSockets.CloseConnection(_conn, 1000, "client shutdown", false);

            _conn.m_HSteamNetConnection = 0;
            Instance = null;
        }

        public void ToggleReady()
        {
            _readySelf = !_readySelf;
            MelonLogger.Msg($"[MP][Client] Ready = {_readySelf}");
            SendReady(_readySelf);
        }

        // --------------- INCOMING ---------------
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
                {
                    var scene = MsgIO.ReadString(r);
                    MelonLogger.Msg($"[MP][Client] Loading scene '{scene}' (seed={_seed})");
                    SceneManager.LoadScene(scene);
                    break;
                }

                case Op.PlayerState:
                {
                    // Host -> client: render host avatar
                    var pos = MsgIO.ReadVec3(r);
                    var rot = MsgIO.ReadQuat(r);
                    GameHooks.ApplyRemotePlayerState(_hostId, pos, rot);
                    break;
                }
            }
        }

        // --------------- OUTGOING ---------------
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
            if (!IsValid(_conn)) return;

            byte[] payload = ms.ToArray();
            IntPtr ptr = Marshal.AllocHGlobal(payload.Length);
            try
            {
                Marshal.Copy(payload, 0, ptr, payload.Length);
                SteamNetworkingSockets.SendMessageToConnection(
                    _conn,
                    ptr,
                    (uint)payload.Length,
                    0,
                    out long _);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        // --------------- TICK (client -> host) ---------------
        public void Tick()
        {
            var now = Time.unscaledTime;
            if (now - _lastSendTime < 0.1f) return; // ~10 Hz
            _lastSendTime = now;

            if (!GameHooks.TryGetLocalPlayerPos(out var rot)) return;
            var pos = GameHooks.LastPos;

            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.PlayerState);
            MsgIO.WriteVec3(_w, pos);
            MsgIO.WriteQuat(_w, rot);
            _w.Flush();
            SendMsg(_ms);
        }
    }
}
