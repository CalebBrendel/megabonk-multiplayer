// SPDX-License-Identifier: GPL-2.0-or-later
// File: src/Megabonk.Multiplayer/Ready/ReadyManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Megabonk.Multiplayer.Ready
{
    /// <summary>
    /// Tracks per-player "Ready" state and starts the game when everyone is ready.
    /// Works with Steam P2P via simple byte messages (see NetMessages).
    /// </summary>
    internal static class ReadyManager
    {
        // --- Public surface for UI ---
        public static bool LocalReady { get; private set; }
        public static int ReadyCount { get; private set; }
        public static int PlayerCount => _readyByPeer.Count;
        public static bool IsHost { get; private set; }
        public static ulong HostId { get; private set; }
        public static ulong LocalId { get; private set; }

        // --- Internal state ---
        private static readonly Dictionary<ulong, bool> _readyByPeer = new Dictionary<ulong, bool>(8);
        private static bool _initialized;

        /// <summary>Call once when lobby/transport is ready.</summary>
        public static void Init(bool isHost, ulong hostId, ulong localId)
        {
            IsHost = isHost;
            HostId = hostId;
            LocalId = localId;
            _initialized = true;

            // Ensure local presence
            if (!_readyByPeer.ContainsKey(localId))
                _readyByPeer[localId] = false;

            Recount();
            MelonLogger.Msg($"[MP][Ready] Init: isHost={IsHost} host={HostId} local={LocalId}");
        }

        /// <summary>Call on peer connect/disconnect so counts stay correct.</summary>
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

        /// <summary>Called by UI: flips/sets local ready state and notifies host/peers.</summary>
        public static void SetLocalReady(bool ready)
        {
            if (!_initialized) return;

            LocalReady = ready;
            _readyByPeer[LocalId] = ready;
            Recount();

            // Notify network
            var payload = Net.NetMessages.MakeReadyState(LocalId, ready);
            if (IsHost)
            {
                // Host updates self + rebroadcast to all others
                Net.Send.AllExcept(LocalId, payload);
                MaybeStart();
            }
            else
            {
                // Client -> Host
                Net.Send.To(HostId, payload);
            }

            MelonLogger.Msg($"[MP][Ready] Local {(ready ? "READY" : "NOT READY")}  ({ReadyCount}/{PlayerCount})");
        }

        /// <summary>Host & clients call this when any MP packet arrives.</summary>
        public static void HandlePacket(ulong from, ArraySegment<byte> data)
        {
            if (!_initialized) return;

            var span = data; // ArraySegment
            if (span.Count == 0) return;
            var id = (Net.MsgId)span.Array[span.Offset]; // first byte is message id

            switch (id)
            {
                case Net.MsgId.ReadyState:
                {
                    var (peerId, isReady) = Net.NetMessages.ReadReadyState(span);
                    // Trust the declared peerId if host; if client, host will echo authoritative states anyway.
                    _readyByPeer[peerId] = isReady;
                    Recount();

                    // Host echoes to everyone else so all clients stay in sync
                    if (IsHost)
                    {
                        var payload = Net.NetMessages.MakeReadyState(peerId, isReady);
                        Net.Send.AllExcept(peerId, payload);
                        MaybeStart();
                    }
                    break;
                }

                case Net.MsgId.StartGame:
                {
                    // Host tells clients to start the round
                    var scene = Net.NetMessages.ReadStartGame(span);
                    MelonLogger.Msg($"[MP][Ready] StartGame received → loading '{scene}'");
                    SafeLoadScene(scene);
                    break;
                }
            }
        }

        // --- Host-side start condition ---
        private static void MaybeStart()
        {
            if (!IsHost) return;
            if (PlayerCount <= 0) return;

            // All connected peers must be ready
            foreach (var kv in _readyByPeer)
            {
                if (!kv.Value) return;
            }

            // Everyone ready -> start!
            const string sceneName = "GeneratedMap";
            MelonLogger.Msg($"Host: starting co-op on scene '{sceneName}'");
            // Tell clients
            var payload = Net.NetMessages.MakeStartGame(sceneName);
            Net.Send.All(payload);
            // Load locally
            SafeLoadScene(sceneName);
        }

        private static void SafeLoadScene(string scene)
        {
            try
            {
                // If the game has a proper entrypoint, call that instead.
                // Otherwise, fallback to direct SceneManager load:
                SceneManager.LoadScene(scene);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[MP][Ready] Failed to load scene '{scene}': {ex}");
            }
        }

        private static void Recount()
        {
            int count = 0;
            foreach (var kv in _readyByPeer)
                if (kv.Value) count++;
            ReadyCount = count;
        }
    }
}
