using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace MegabonkMultiplayer.Net
{
  public enum OpCode : byte
  {
    Hello = 1,
    PlayerXform = 2,
    FireEvent = 3,
    Spawn = 4,
    Despawn = 5,
    EnemyHP = 6
  }

  public static class NetCommon
  {
    public static EventBasedNetListener Listener;
    public static NetManager Manager;
    public static NetDataWriter Writer = new NetDataWriter();

    /// <summary>
    /// Initialize networking. Tries to bind to port 27015; if it fails (e.g. client on same PC),
    /// it falls back to an ephemeral port so outgoing connections still work.
    /// Call once on game start (host and client both call this).
    /// </summary>
    public static void Init()
    {
      if (Manager != null) return; // already initialized

      Listener = new EventBasedNetListener();
      Manager  = new NetManager(Listener)
      {
        IPv6Enabled = false,
        AutoRecycle = true,
        UnsyncedEvents = true
      };

      // Try the default port; if unavailable, fall back to ephemeral (client case).
      bool started = Manager.Start(27015);
      if (!started)
      {
        // Ephemeral bind (OS chooses the port). Outgoing connections will still work for clients.
        Manager.Start();
      }
    }

    /// <summary>
    /// Poll LiteNetLib events (call every frame from your mod Update).
    /// </summary>
    public static void Poll()
    {
      Manager?.PollEvents();
    }

    /// <summary>
    /// Reset transient state between runs (expand as you add entity maps, ids, etc).
    /// </summary>
    public static void ResetForRun()
    {
      // future: clear entity maps, sequence ids, pending RPCs, etc.
    }
  }

  public static class Host
  {
    static readonly List<NetPeer> _peers = new();

    // Wire up host-side listener behavior once at first use
    static bool _wired;
    static void EnsureWired()
    {
      if (_wired) return;
      _wired = true;

      var l = NetCommon.Listener;

      // Simple key to avoid random connections during tests
      l.ConnectionRequestEvent += req => req.AcceptIfKey("mbonk");

      l.PeerConnectedEvent += peer =>
      {
        if (!_peers.Contains(peer)) _peers.Add(peer);
      };

      l.PeerDisconnectedEvent += (peer, info) =>
      {
        _peers.Remove(peer);
      };

      // Receive client → host messages (e.g., client inputs later)
      l.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        var op = (OpCode)reader.GetByte();
        switch (op)
        {
          case OpCode.Hello:
            // Optionally ACK or send initial state
            break;

          // case OpCode.FireEvent:
          //   // apply client fire intent to authoritative sim
          //   break;
        }
        reader.Recycle();
      };
    }

    public static void Tick()
    {
      EnsureWired();
      NetCommon.Poll();
      // (Authoritative sim lives here; drive world, then broadcast deltas)
    }

    public static void BroadcastPlayerTransform(Vector3 pos, Quaternion rot)
    {
      if (_peers.Count == 0) return;

      var w = NetCommon.Writer;
      w.Reset();
      w.Put((byte)OpCode.PlayerXform);
      w.Put(pos.x); w.Put(pos.y); w.Put(pos.z);
      w.Put(rot.x); w.Put(rot.y); w.Put(rot.z); w.Put(rot.w);

      // Unreliable is fine for frequent pose updates
      for (int i = 0; i < _peers.Count; i++)
        _peers[i].Send(w, DeliveryMethod.Unreliable);
    }

    // Example stubs for later:
    // public static void BroadcastSpawn(int typeId, Vector3 pos, int seed) { ... }
    // public static void BroadcastDespawn(int entityId) { ... }
    // public static void BroadcastEnemyHp(int entityId, float hp) { ... }
  }

  public static class Client
  {
    static Vector3 _pos;
    static Quaternion _rot;
    static bool _havePose;
    static NetPeer _peer;

    // Wire once
    static bool _wired;
    static void EnsureWired()
    {
      if (_wired) return;
      _wired = true;

      var l = NetCommon.Listener;

      l.PeerConnectedEvent += p =>
      {
        _peer = p;

        // Send a quick hello
        var w = NetCommon.Writer;
        w.Reset();
        w.Put((byte)OpCode.Hello);
        p.Send(w, DeliveryMethod.ReliableOrdered);
      };

      l.NetworkReceiveEvent += (peer, reader, channel, method) =>
      {
        var op = (OpCode)reader.GetByte();
        switch (op)
        {
          case OpCode.PlayerXform:
            _pos = new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            _rot = new Quaternion(reader.GetFloat(), reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
            _havePose = true;
            break;

          // case OpCode.Spawn: ...
          // case OpCode.Despawn: ...
          // case OpCode.EnemyHP: ...
        }
        reader.Recycle();
      };
    }

    static void EnsureConnected(string addr = "127.0.0.1", int port = 27015)
    {
      if (_peer != null && _peer.ConnectionState == ConnectionState.Connected) return;

      // Connect using the same key the host accepts
      if (NetCommon.Manager.ConnectedPeerList.Count == 0)
      {
        NetCommon.Manager.Connect(addr, port, "mbonk");
      }
    }

    public static void Tick()
    {
      EnsureWired();
      EnsureConnected();
      NetCommon.Poll();
    }

    public static bool TryGetHostTransform(out Vector3 pos, out Quaternion rot)
    {
      pos = _pos;
      rot = _rot;
      return _havePose;
    }

    // Example for later:
    // public static void SendFireEvent(...) { ... }
  }
}
