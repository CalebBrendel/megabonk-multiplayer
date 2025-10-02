// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/Ready/ReadyManager.cs
using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine.SceneManagement;
using Megabonk.Multiplayer.Net;

namespace Megabonk.Multiplayer.Ready
{
    /// <summary>
    /// Tracks per-player "Ready" state across the lobby. Host auto-starts when all are ready.
    /// </summary>
    internal static class ReadyManager
    {
        // Public surface (read from UI)
        public static bool LocalReady { get; private set; }
        public static int ReadyCount { get; private set; }
        public static int PlayerCount => _readyByPeer.Count;
        public static bool IsHost { get; private set; }
        public static ulong HostId { get; private set; }
        public static ulong LocalId { get; private set; }

        // Internal
        private static readonly Dictionary<ulong, bool> _readyByPeer = new Dictionary<ulong, bool>(8);
        private static bool _initialized;

        public static void Init(bool isHost, ulong hostId, ulong localId)
        {
            IsHost  = isHost;
            HostId  = hostId;
            LocalId = localId;
            _initialized = true;

            if (!_readyByPeer.ContainsKey(localId))
                _readyByPeer[localId] = false;

            Recount();
            MelonLogger.Msg($"[MP][Ready] Init: isHost={IsHost} host={HostId} local={LocalId}");
        }

        public static void OnPeerConnected(ulong peerId)
        {
            if (!_initialized) return;
            if (!_readyByPeer.ContainsKey(peerId))
                _readyByPeer[peerId] = false;
            Recount();
            MelonLogger.Msg($"[MP][Ready] Peer connected: {peerId}  ({ReadyCount}/{PlayerCount} ready)");
            if (IsHost) MaybeStart();
        }

        public static void OnPeerDisconnected(ulong peerId)
        {
            if (!_initialized) return;
            if (_readyByPeer.Remove(peerId))
            {
                Recount();
                MelonLogger.Msg($"[MP][Ready] Peer disconnected: {peerId}  ({ReadyCount}/{PlayerCount} ready)");
            }
        }

        /// <summary>Called by UI to (un)ready locally; will notify host/peers.</summary>
        public static void SetLocalReady(bool ready)
        {
            if (!_initialized) return;

            LocalReady = ready;
            _readyByPeer[LocalId] = ready;
            Recount();

            var payload = NetMessages.MakeReadyState(LocalId, ready);
            if (IsHost)
            {
                Send.AllExcept(LocalId, payload);
                MaybeStart();
            }
            else
            {
                Send.To(HostId, payload);
            }

            MelonLogger.Msg($"[MP][Ready] Local {(ready ? "READY" : "NOT READY")}  ({ReadyCount}/{PlayerCount})");
        }

        /// <summary>Route our tiny protocol messages here from your receive loop.</summary>
        public static void HandlePacket(ulong from, ArraySegment<byte> data)
        {
            if (!_initialized || data.Count <= 0) return;

            var msgId = (MsgId)data.Array![data.Offset];
            switch (msgId)
            {
                case MsgId.ReadyState:
                {
                    var (peerId, isReady) = NetMessages.ReadReadyState(data);
                    _readyByPeer[peerId] = isReady;
                    Recount();

                    if (IsHost)
                    {
                        var echo = NetMessages.MakeReadyState(peerId, isReady);
                        Send.AllExcept(peerId, echo);
                        MaybeStart();
                    }
                    break;
                }

                case MsgId.StartGame:
                {
                    var scene = NetMessages.ReadStartGame(data);
                    MelonLogger.Msg($"[MP][Ready] StartGame received → loading '{scene}'");
                    SafeLoadScene(scene);
                    break;
                }
            }
        }

        // Host-side: start when everyone ready
        private static void MaybeStart()
        {
            if (!IsHost || PlayerCount <= 0) return;

            foreach (var kv in _readyByPeer)
                if (!kv.Value) return; // someone not ready

            const string sceneName = "GeneratedMap";
            MelonLogger.Msg($"Host: starting co-op on scene '{sceneName}'");

            var payload = NetMessages.MakeStartGame(sceneName);
            Send.All(payload);
            SafeLoadScene(sceneName);
        }

        private static void SafeLoadScene(string scene)
        {
            try { SceneManager.LoadScene(scene); }
            catch (Exception ex) { MelonLogger.Error($"[MP][Ready] Failed to load scene '{scene}': {ex}"); }
        }

        private static void Recount()
        {
            int c = 0;
            foreach (var kv in _readyByPeer) if (kv.Value) c++;
            ReadyCount = c;
        }
    }
}
