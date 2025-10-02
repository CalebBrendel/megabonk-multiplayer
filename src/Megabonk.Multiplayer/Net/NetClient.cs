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

        // store the raw int handle; wrap it when calling Steam
        private int _connHandle;
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

            // In your Steamworks.NET build, this returns an int handle (per compiler error).
            _connHandle = SteamNetworkingSockets.ConnectP2P(ref ident, 0, cfg.Length, cfg);
            MelonLogger.Msg("[Megabonk Multiplayer] Client connecting to host...");
        }

        private static bool IsValid(int handle) => handle != 0;
        private static HSteamNetConnection Wrap(int handle)
        {
            // HSteamNetConnection is a struct with an int field in Steamworks.NET
            var h = new HSteamNetConnection();
            h.m_HSteamNetConnection = handle;
            return h;
        }

        public void Shutdown()
        {
            if (IsValid(_connHandle))
            {
                SteamNetworkingSockets.CloseConnection(Wrap(_connHandle), 1000, "client shutdown", false);
            }
            _connHandle = 0;
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
                {
                    var scene = MsgIO.ReadString(r);
                    MelonLogger.Msg($"[MP][Client] Loading scene '{scene}' (seed={_seed})");
                    SceneManager.LoadScene(scene);
                    break;
                }
                case Op.PlayerState:
                {
                    var pos = MsgIO.ReadVec3(r);
                    var rot = MsgIO.ReadQuat(r);
                    GameHooks.ApplyRemotePlayerState(SteamUser.GetSteamID(), pos, rot);
                    break;
                }
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
            if (!IsValid(_connHandle)) return;

            byte[] payload = ms.ToArray();
            IntPtr ptr = Marshal.AllocHGlobal(payload.Length);
            try
            {
                Marshal.Copy(payload, 0, ptr, payload.Length);
                SteamNetworkingSockets.SendMessageToConnection(
                    Wrap(_connHandle),
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
