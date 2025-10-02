using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer.Net
{
    public class NetHost
    {
        public static NetHost Instance { get; private set; }

        private readonly Dictionary<CSteamID, HSteamNetConnection> _conns = new();
        private readonly Dictionary<CSteamID, bool> _ready = new();
        private readonly MemoryStream _ms = new MemoryStream(256);
        private readonly BinaryWriter _w;
        private float _lastSendTime;

        private int _seed = 0;

        public bool ReadySelf { get; private set; }

        public static void StartListening()
        {
            Instance?.Shutdown();
            Instance = new NetHost();
            Instance.Init();
        }

        private NetHost()
        {
            _w = new BinaryWriter(_ms);
        }

        private void Init()
        {
            MelonLogger.Msg("[Megabonk Multiplayer] NetHost: listening P2P");
            SteamNetworkingUtils.InitRelayNetworkAccess();
            // The actual accept happens via SteamLobby's connection status callback.
        }

        public void OnNewConnection(CSteamID peer, HSteamNetConnection hconn)
        {
            if (_conns.ContainsKey(peer)) return;
            _conns[peer] = hconn;
            _ready[peer] = false;
            MelonLogger.Msg($"[MP][Host] Peer connected: {peer.m_SteamID}");
            SendHello(hconn);
            BroadcastReadySummary();
        }

        public void OnDisconnect(CSteamID peer)
        {
            if (_conns.Remove(peer))
                MelonLogger.Msg($"[MP][Host] Peer disconnected: {peer.m_SteamID}");
            _ready.Remove(peer);
            BroadcastReadySummary();
        }

        public void Shutdown()
        {
            foreach (var kv in _conns)
                SteamNetworkingSockets.CloseConnection(kv.Value, 1000, "host shutdown", false);
            _conns.Clear();
            _ready.Clear();
            Instance = null;
        }

        // ---- READY ----
        public void ToggleReady()
        {
            ReadySelf = !ReadySelf;
            MelonLogger.Msg($"[MP][Host] Ready = {ReadySelf}");
            BroadcastReadySummary();
        }

        public (int ready, int total) GetReadyCounts()
        {
            int ready = ReadySelf ? 1 : 0;
            int total = 1; // include host
            foreach (var kv in _ready)
            {
                total++;
                if (kv.Value) ready++;
            }
            return (ready, total);
        }

        public bool AllReady()
        {
            foreach (var kv in _ready)
                if (!kv.Value) return false;
            return ReadySelf && true;
        }

        private void BroadcastReadySummary()
        {
            var (r, t) = GetReadyCounts();
            MelonLogger.Msg($"[MP][Host] Ready {r}/{t}");
        }

        public void OnMsg(CSteamID from, Op op, BinaryReader r)
        {
            switch (op)
            {
                case Op.Ready:
                {
                    bool val = r.ReadBoolean();
                    _ready[from] = val;
                    MelonLogger.Msg($"[MP][Host] Client {from.m_SteamID} Ready={val}");
                    BroadcastReadySummary();
                    break;
                }
            }
        }

        // ---- LOAD / SEED ----
        public void StartCoop(string sceneName)
        {
            _seed = (int)((DateTime.UtcNow.Ticks ^ (long)SteamUser.GetSteamID().m_SteamID) & 0x7fffffff);
            UnityEngine.Random.InitState(_seed);

            foreach (var kv in _conns)
            {
                SendSeed(kv.Value, _seed);
                SendLoadLevel(kv.Value, sceneName);
            }

            MelonLogger.Msg($"[MP][Host] Starting co-op: scene='{sceneName}', seed={_seed}");
            SceneManager.LoadScene(sceneName);
        }

        public void BroadcastLoadLevel(string sceneName)
        {
            foreach (var kv in _conns)
                SendLoadLevel(kv.Value, sceneName);
        }

        private void SendHello(HSteamNetConnection to)
        {
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.Hello);
            _w.Flush();
            SendMsg(to, _ms);
        }

        private void SendSeed(HSteamNetConnection to, int seed)
        {
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.SeedSync);
            _w.Write(seed);
            _w.Flush();
            SendMsg(to, _ms);
        }

        private void SendLoadLevel(HSteamNetConnection to, string sceneName)
        {
            _ms.Position = 0; _ms.SetLength(0);
            MsgIO.WriteHeader(_w, Op.LoadLevel);
            MsgIO.WriteString(_w, sceneName);
            _w.Flush();
            SendMsg(to, _ms);
        }

        private static void SendMsg(HSteamNetConnection to, MemoryStream ms)
        {
            byte[] payload = ms.ToArray();
            IntPtr ptr = Marshal.AllocHGlobal(payload.Length);
            try
            {
                Marshal.Copy(payload, 0, ptr, payload.Length);
                SteamNetworkingSockets.SendMessageToConnection(
                    to,
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

        public void Tick()
        {
            var now = Time.unscaledTime;
            if (now - _lastSendTime < 0.1f) return; // ~10 Hz
            _lastSendTime = now;

            if (!HarmonyPatches.GameHooks.TryGetLocalPlayerPos(out var rot)) return;
            var pos = HarmonyPatches.GameHooks.LastPos;

            foreach (var kv in _conns)
            {
                _ms.Position = 0; _ms.SetLength(0);
                MsgIO.WriteHeader(_w, Op.PlayerState);
                MsgIO.WriteVec3(_w, pos);
                MsgIO.WriteQuat(_w, rot);
                _w.Flush();
                SendMsg(kv.Value, _ms);
            }
        }
    }
}
